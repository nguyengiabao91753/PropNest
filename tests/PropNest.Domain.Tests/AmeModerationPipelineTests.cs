using System.Net;
using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;
using PropNest.Domain.Listings;
using PropNest.Domain.Users;
using PropNest.Domain.Workflows;
using PropNest.Infrastructure.Consumers;
using PropNest.Infrastructure.Persistence;
using PropNest.Infrastructure.Persistence.Repositories;
using PropNest.Infrastructure.Services;

namespace PropNest.Domain.Tests;

public sealed class AmeModerationPipelineTests
{
    [Fact]
    public async Task SubmitAndUpdateListingShouldEnqueueListingSubmittedAndPublishViaMassTransit()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var repository = new ListingRepository(dbContext);
        var service = new ListingService(repository, dbContext);

        // 1. Tạo tin đăng mới (Draft)
        var created = await service.CreateAsync(new CreateListingCommand(
            OwnerUserId: 1,
            Title: "Căn hộ Quận 7 view sông",
            Description: "Căn hộ 2 phòng ngủ đầy đủ nội thất tại Quận 7.",
            PropertyType: "Apartment",
            ListingType: "Sale",
            Price: 4_800_000_000m,
            Area: 75m,
            City: "TP. Hồ Chí Minh",
            District: "Quận 7",
            Ward: "Tân Phú",
            Address: "10 Nguyễn Văn Linh"));

        // 2. Submit tin đăng -> Phải sinh OutboxMessage("ListingSubmitted")
        await service.SubmitAsync(created.ListingId, 1);

        var outboxMessages = await dbContext.OutboxMessages
            .Where(x => x.Type == "ListingSubmitted")
            .ToListAsync();

        var submitOutbox = Assert.Single(outboxMessages);
        Assert.NotNull(submitOutbox.CorrelationId);

        // 3. Kiểm tra MassTransitOutboxPublisher phát sự kiện ListingSubmitted lên IPublishEndpoint
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var publisher = new MassTransitOutboxPublisher(
            publishEndpointMock.Object,
            NullLogger<MassTransitOutboxPublisher>.Instance);

        await publisher.PublishAsync(submitOutbox);

        publishEndpointMock.Verify(
            x => x.Publish(
                It.Is<ListingSubmitted>(e =>
                    e.ListingId == created.ListingId &&
                    e.CorrelationId == submitOutbox.CorrelationId &&
                    e.Title == "Căn hộ Quận 7 view sông"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AmeModerationClientShouldSendCorrelationIdAndFallbackOnTimeoutOrError()
    {
        var correlationId = Guid.NewGuid();
        string? capturedCorrelationHeader = null;

        // 1. Trường hợp FastAPI trả về 200 OK
        using var okHandler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.Headers.TryGetValues("X-Correlation-ID", out var values))
            {
                capturedCorrelationHeader = values.FirstOrDefault();
            }

            var json = """
                {
                  "decision": "Approved",
                  "totalRiskScore": 12.5,
                  "keywordRiskScore": 0,
                  "priceAnomalyScore": 5.0,
                  "completenessScore": 95.0,
                  "duplicateScore": 0,
                  "aiSemanticScore": 10.0,
                  "summary": "Tin đăng hợp lệ, giá nằm trong biên độ Quận 7.",
                  "processingTimeMs": 145,
                  "reasonCodes": ["CLEAN_CONTENT", "PRICE_WITHIN_BENCHMARK"]
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        using var okHttpClient = new HttpClient(okHandler) { BaseAddress = new Uri("http://localhost:8001") };
        var enabledOptions = Options.Create(new AmeOptions { BaseUrl = "http://localhost:8001", TimeoutSeconds = 15, Enabled = true });
        var client = new AmeModerationClient(okHttpClient, enabledOptions, NullLogger<AmeModerationClient>.Instance);

        var requestDto = CreateSampleAmeRequest(correlationId);
        var okResponse = await client.ModerateAsync(requestDto);

        Assert.Equal(correlationId.ToString(), capturedCorrelationHeader);
        Assert.Equal(ModerationDecision.Approved, okResponse.Decision);
        Assert.Equal(12.5m, okResponse.TotalRiskScore);
        Assert.Contains("CLEAN_CONTENT", okResponse.ReasonCodes);

        // 2. Trường hợp FastAPI bị Timeout / Lỗi kết nối -> Fallback sang ManualReview
        using var timeoutHandler = new StubHttpMessageHandler((_, _) =>
            throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 15 seconds elapsing."));
        using var timeoutHttpClient = new HttpClient(timeoutHandler) { BaseAddress = new Uri("http://localhost:8001") };
        var fallbackClient = new AmeModerationClient(timeoutHttpClient, enabledOptions, NullLogger<AmeModerationClient>.Instance);

        var fallbackResponse = await fallbackClient.ModerateAsync(requestDto);
        Assert.Equal(ModerationDecision.ManualReview, fallbackResponse.Decision);
        Assert.Contains("AME_UNAVAILABLE_FALLBACK_MANUAL", fallbackResponse.ReasonCodes);
    }

    [Fact]
    public async Task AmeModerationConsumerShouldAutoApproveValidListingAndRecordAuditTrail()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var listing = await SeedPendingListingAsync(dbContext);
        var correlationId = Guid.NewGuid();

        var ameClientMock = new Mock<IAmeModerationClient>();
        ameClientMock
            .Setup(x => x.ModerateAsync(It.IsAny<AmeModerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AmeModerationResponse(
                Decision: ModerationDecision.Approved,
                TotalRiskScore: 10m,
                KeywordRiskScore: 0m,
                PriceAnomalyScore: 5m,
                CompletenessScore: 95m,
                DuplicateScore: 0m,
                AiSemanticScore: 8m,
                Summary: "Tin đăng đạt chuẩn chất lượng tự động.",
                ProcessingTimeMs: 120,
                ReasonCodes: ["PASS_ALL_RULES"],
                RawResponseJson: "{\"decision\":\"Approved\"}"));

        var repository = new ListingRepository(dbContext);
        var consumer = new AmeModerationConsumer(
            dbContext,
            repository,
            ameClientMock.Object,
            dbContext,
            NullLogger<AmeModerationConsumer>.Instance);

        var consumeContextMock = CreateConsumeContext(listing, correlationId);
        await consumer.Consume(consumeContextMock.Object);

        var updatedListing = await dbContext.Listings.SingleAsync(x => x.ListingId == listing.ListingId);
        Assert.Equal(ListingStatus.PendingPayment, updatedListing.Status);
        Assert.Equal(ModerationDecision.Approved, updatedListing.ModerationDecision);
        Assert.Null(updatedListing.StartDate);
        Assert.Null(updatedListing.EndDate);

        var ameResult = await dbContext.AmeModerationResults.SingleAsync(x => x.CorrelationId == correlationId);
        Assert.Equal(ModerationDecision.Approved, ameResult.Decision);

        var review = await dbContext.ListingModerationReviews.SingleAsync(x => x.ListingId == listing.ListingId);
        Assert.Equal("AME", review.Source);
        Assert.Equal(ModerationDecision.Approved, review.Decision);
        Assert.Null(review.ReviewerUserId);

        var approveHistory = await dbContext.ListingHistories.SingleAsync(h => h.ListingId == listing.ListingId && h.ActionType == "Approve");
        Assert.Equal("Tin đăng đã được duyệt.", approveHistory.Note);
        Assert.DoesNotContain("AME", approveHistory.Note ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.True(await dbContext.OutboxMessages.AnyAsync(o => o.Type == "ListingApprovedPendingPayment"));
    }

    [Fact]
    public async Task AmeModerationConsumerShouldAutoApproveEditedPaidListingToPublished()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var listing = await SeedPendingListingAsync(dbContext);
        var now = DateTimeOffset.UtcNow;
        listing.Approve(now);
        listing.ApplyPackagePurchase("Standard", 15, now);
        var expectedEndDate = listing.EndDate;
        listing.UpdateContent(
            "Căn hộ cao cấp Quận 7 (cập nhật)",
            "Cập nhật nội thất mới hoàn toàn, dọn vào ở ngay.",
            "Apartment",
            "Sale",
            4_900_000_000m,
            75m,
            "TP. Hồ Chí Minh",
            "Quận 7",
            "Tân Phú",
            "1 Nguyễn Văn Linh");
        await dbContext.SaveChangesAsync();

        var correlationId = Guid.NewGuid();
        var ameClientMock = new Mock<IAmeModerationClient>();
        ameClientMock
            .Setup(x => x.ModerateAsync(It.IsAny<AmeModerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AmeModerationResponse(
                Decision: ModerationDecision.Approved,
                TotalRiskScore: 8m,
                KeywordRiskScore: 0m,
                PriceAnomalyScore: 3m,
                CompletenessScore: 95m,
                DuplicateScore: 0m,
                AiSemanticScore: 5m,
                Summary: "Nội dung cập nhật hợp lệ.",
                ProcessingTimeMs: 95,
                ReasonCodes: ["PASS_ALL_RULES"],
                RawResponseJson: "{\"decision\":\"Approved\"}"));

        var repository = new ListingRepository(dbContext);
        var consumer = new AmeModerationConsumer(
            dbContext,
            repository,
            ameClientMock.Object,
            dbContext,
            NullLogger<AmeModerationConsumer>.Instance);

        var consumeContextMock = CreateConsumeContext(listing, correlationId);
        await consumer.Consume(consumeContextMock.Object);

        var updatedListing = await dbContext.Listings.SingleAsync(x => x.ListingId == listing.ListingId);
        Assert.Equal(ListingStatus.Published, updatedListing.Status);
        Assert.Equal(ModerationDecision.Approved, updatedListing.ModerationDecision);
        Assert.Equal(expectedEndDate, updatedListing.EndDate);
        Assert.True(await dbContext.OutboxMessages.AnyAsync(o => o.Type == "ListingPublished"));
    }

    [Fact]
    public async Task AmeModerationConsumerShouldAutoRejectViolatingListing()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var listing = await SeedPendingListingAsync(dbContext);
        var correlationId = Guid.NewGuid();

        var ameClientMock = new Mock<IAmeModerationClient>();
        ameClientMock
            .Setup(x => x.ModerateAsync(It.IsAny<AmeModerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AmeModerationResponse(
                Decision: ModerationDecision.Rejected,
                TotalRiskScore: 100m,
                KeywordRiskScore: 100m,
                PriceAnomalyScore: 0m,
                CompletenessScore: 60m,
                DuplicateScore: 0m,
                AiSemanticScore: null,
                Summary: "Phát hiện từ khóa cấm hoặc lộ số điện thoại trong mô tả.",
                ProcessingTimeMs: 18,
                ReasonCodes: ["RULE_1_BADWORD", "RULE_3_PHONE_EXPOSURE"],
                RawResponseJson: "{\"decision\":\"Rejected\"}"));

        var repository = new ListingRepository(dbContext);
        var consumer = new AmeModerationConsumer(
            dbContext,
            repository,
            ameClientMock.Object,
            dbContext,
            NullLogger<AmeModerationConsumer>.Instance);

        var consumeContextMock = CreateConsumeContext(listing, correlationId);
        await consumer.Consume(consumeContextMock.Object);

        var updatedListing = await dbContext.Listings.SingleAsync(x => x.ListingId == listing.ListingId);
        Assert.Equal(ListingStatus.Rejected, updatedListing.Status);
        Assert.Equal(ModerationDecision.Rejected, updatedListing.ModerationDecision);

        var review = await dbContext.ListingModerationReviews.SingleAsync(x => x.ListingId == listing.ListingId);
        Assert.Equal("AME", review.Source);
        Assert.Equal(ModerationDecision.Rejected, review.Decision);

        var rejectHistory = await dbContext.ListingHistories.SingleAsync(h => h.ListingId == listing.ListingId && h.ActionType == "Reject");
        Assert.DoesNotContain("AME", rejectHistory.Note ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.True(await dbContext.OutboxMessages.AnyAsync(o => o.Type == "ListingRejected"));
    }

    [Fact]
    public async Task AmeModerationConsumerShouldRouteToManualReviewWhenAmeFallsBack()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var listing = await SeedPendingListingAsync(dbContext);
        var correlationId = Guid.NewGuid();

        var ameClientMock = new Mock<IAmeModerationClient>();
        ameClientMock
            .Setup(x => x.ModerateAsync(It.IsAny<AmeModerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AmeModerationResponse.FallbackManualReview("AME_UNAVAILABLE_FALLBACK_MANUAL", "Connection refused"));

        var repository = new ListingRepository(dbContext);
        var consumer = new AmeModerationConsumer(
            dbContext,
            repository,
            ameClientMock.Object,
            dbContext,
            NullLogger<AmeModerationConsumer>.Instance);

        var consumeContextMock = CreateConsumeContext(listing, correlationId);
        await consumer.Consume(consumeContextMock.Object);

        var updatedListing = await dbContext.Listings.SingleAsync(x => x.ListingId == listing.ListingId);
        Assert.Equal(ListingStatus.PendingModeration, updatedListing.Status);
        Assert.Equal(ModerationDecision.ManualReview, updatedListing.ModerationDecision);

        var ameResult = await dbContext.AmeModerationResults.SingleAsync(x => x.CorrelationId == correlationId);
        Assert.Equal(ModerationDecision.ManualReview, ameResult.Decision);
        Assert.DoesNotContain("Connection refused", ameResult.ReasonsJson ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        // AME không duyệt được và đẩy về Admin duyệt thủ công -> KHÔNG ghi ListingHistory
        Assert.False(await dbContext.ListingHistories.AnyAsync(h => h.ListingId == listing.ListingId));
    }

    [Fact]
    public async Task ListingRepositoryShouldExcludeAmePendingAndShowOnlyManualReviewInAdminQueue()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var awaitingAme = new Listing(
            ownerUserId: 1,
            title: "Tin đang chờ AME quét",
            description: "Nội dung đầy đủ đang được AME xử lý tự động.",
            propertyType: "Apartment",
            listingType: "Sale",
            price: 3_500_000_000m,
            area: 70m,
            city: "Tỉnh Bắc Ninh",
            district: "",
            ward: "Phường Việt Yên",
            address: "Evergreen Việt Yên",
            administrativeMode: "PostMerger",
            provinceCode: 24,
            legacyAddress: "Thành phố Bắc Ninh, Tỉnh Bắc Ninh");
        awaitingAme.SubmitForModeration(); // ModerationDecision = Pending

        var escalatedToAdmin = new Listing(
            ownerUserId: 1,
            title: "Tin AME đẩy sang Admin do ảnh mờ",
            description: "Mô tả hợp lệ nhưng ảnh cần kiểm duyệt viên xác minh.",
            propertyType: "House",
            listingType: "Sale",
            price: 5_200_000_000m,
            area: 80m,
            city: "Thành phố Hồ Chí Minh",
            district: "Quận 7",
            ward: "Tân Phú",
            address: "25 Nguyễn Văn Linh");
        escalatedToAdmin.SubmitForModeration();
        escalatedToAdmin.MarkForManualReview(); // ModerationDecision = ManualReview

        dbContext.Listings.AddRange(awaitingAme, escalatedToAdmin);
        await dbContext.SaveChangesAsync();

        var repository = new ListingRepository(dbContext);
        var entityQueue = await repository.GetPendingModerationAsync();
        var dtoQueue = await repository.GetPendingModerationWithOwnersAsync();

        var singleEntity = Assert.Single(entityQueue);
        Assert.Equal(escalatedToAdmin.ListingId, singleEntity.ListingId);
        Assert.DoesNotContain(entityQueue, x => x.ListingId == awaitingAme.ListingId);

        var singleDto = Assert.Single(dtoQueue);
        Assert.Equal(escalatedToAdmin.ListingId, singleDto.ListingId);
        Assert.Equal(ModerationDecision.ManualReview, singleDto.ModerationDecision);
    }

    [Fact]
    public async Task AmeModerationConsumerShouldAutoApproveWithPriceWarningAndExposeWarningOnListingDto()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var listing = new Listing(
            ownerUserId: 1,
            title: "Căn hộ Quận 7 giá cực rẻ bất thường",
            description: "Căn hộ 2 phòng ngủ đầy đủ nội thất, sổ hồng chính chủ tại Quận 7.",
            propertyType: "Apartment",
            listingType: "Sale",
            price: 300_000_000m, // 4 triệu/m² cho 75m² -> quá thấp so với bình quân 65 triệu/m² Q7
            area: 75m,
            city: "Thành phố Hồ Chí Minh",
            district: "",
            ward: "Phường Tân Phú",
            address: "10 Nguyễn Văn Linh",
            administrativeMode: "PostMerger",
            provinceCode: 79,
            legacyAddress: "Quận 7, Thành phố Hồ Chí Minh");
        listing.SubmitForModeration();
        dbContext.Listings.Add(listing);
        await dbContext.SaveChangesAsync();

        var correlationId = Guid.NewGuid();
        AmeModerationRequest? capturedRequest = null;

        var ameClientMock = new Mock<IAmeModerationClient>();
        ameClientMock
            .Setup(x => x.ModerateAsync(It.IsAny<AmeModerationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AmeModerationRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new AmeModerationResponse(
                Decision: ModerationDecision.Approved,
                TotalRiskScore: 36m,
                KeywordRiskScore: 0m,
                PriceAnomalyScore: 80m,
                CompletenessScore: 95m,
                DuplicateScore: 0m,
                AiSemanticScore: 10m,
                Summary: "Thông tin & hình ảnh hợp lệ (tự động duyệt). Cảnh báo: Giá đăng ~4.0 triệu đ/m² thấp hơn đáng kể so với giá bình quân khu vực (~65.0 triệu đ/m²). Giá tin đăng này có thể là giá ảo.",
                ProcessingTimeMs: 110,
                ReasonCodes: ["PRICE_TOO_LOW", "PASS_CONTENT_AND_IMAGE"],
                RawResponseJson: "{\"decision\":\"Approved\"}",
                Violations:
                [
                    new AmeRuleViolationDto("PRICE_TOO_LOW", "price_warning", "Đơn giá ~4.0 triệu đ/m² thấp hơn đáng kể so với giá bình quân khu vực (~65.0 triệu đ/m²). Giá tin đăng này có thể là giá ảo.")
                ],
                PriceWarningMessage: "Đơn giá ~4.0 triệu đ/m² thấp hơn đáng kể so với giá bình quân khu vực (~65.0 triệu đ/m²). Giá tin đăng này có thể là giá ảo."));

        var repository = new ListingRepository(dbContext);
        var consumer = new AmeModerationConsumer(
            dbContext,
            repository,
            ameClientMock.Object,
            dbContext,
            NullLogger<AmeModerationConsumer>.Instance);

        var consumeContextMock = CreateConsumeContext(listing, correlationId);
        await consumer.Consume(consumeContextMock.Object);

        // Xác nhận tìm được benchmark Q7 dù City là "Thành phố Hồ Chí Minh" và Quận nằm trong LegacyAddress
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest!.PriceBenchmark);
        Assert.Equal("Quận 7", capturedRequest.PriceBenchmark!.District);
        Assert.Equal(65_000_000m, capturedRequest.PriceBenchmark.AvgPricePerSqm);

        var updatedListing = await dbContext.Listings.SingleAsync(x => x.ListingId == listing.ListingId);
        Assert.Equal(ListingStatus.PendingPayment, updatedListing.Status);
        Assert.Equal(ModerationDecision.Approved, updatedListing.ModerationDecision);

        // History dùng ActionType thống nhất "Approve" và ghi chú giá ảo bằng văn bản thuần
        var history = await dbContext.ListingHistories
            .SingleAsync(h => h.ListingId == listing.ListingId && h.ActionType == "Approve");
        Assert.Contains("giá ảo", history.Note, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[Cảnh báo", history.Note ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        // ListingService phải trả về LatestModerationWarning cho Seller xem
        var listingService = new ListingService(repository, dbContext);
        var dto = await listingService.GetByIdAsync(listing.ListingId);
        Assert.NotNull(dto);
        Assert.Contains("giá ảo", dto!.LatestModerationWarning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AmeModerationClientShouldParseCamelCasePriceWarningAndViolations()
    {
        var correlationId = Guid.NewGuid();
        const string json = """
            {
              "listingId": 100,
              "decision": "Approved",
              "source": "AI-Aggregated",
              "score": 0.85,
              "totalRiskScore": 36.0,
              "keywordRiskScore": 0.0,
              "priceAnomalyScore": 80.0,
              "completenessScore": 95.0,
              "duplicateScore": 0.0,
              "aiSemanticScore": 10.0,
              "summary": "Tin hợp lệ. Cảnh báo giá ảo.",
              "processingTimeMs": 92,
              "reasonCodes": ["PRICE_TOO_HIGH", "PASS_CONTENT_AND_IMAGE"],
              "priceWarningMessage": "Đơn giá cao hơn đáng kể so với khu vực. Giá tin đăng này có thể là giá ảo.",
              "violations": [
                {
                  "rule": "PRICE_TOO_HIGH",
                  "severity": "price_warning",
                  "message": "Đơn giá cao hơn đáng kể so với khu vực. Giá tin đăng này có thể là giá ảo."
                }
              ]
            }
            """;

        using var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8001") };
        var client = new AmeModerationClient(
            httpClient,
            Options.Create(new AmeOptions { BaseUrl = "http://localhost:8001", TimeoutSeconds = 15, Enabled = true }),
            NullLogger<AmeModerationClient>.Instance);

        var response = await client.ModerateAsync(CreateSampleAmeRequest(correlationId));

        Assert.Equal(ModerationDecision.Approved, response.Decision);
        Assert.Equal(80m, response.PriceAnomalyScore);
        Assert.Equal("Đơn giá cao hơn đáng kể so với khu vực. Giá tin đăng này có thể là giá ảo.", response.PriceWarningMessage);
        Assert.NotNull(response.Violations);
        var v = Assert.Single(response.Violations!);
        Assert.Equal("PRICE_TOO_HIGH", v.Rule);
    }

    private static async Task<PropNestDbContext> CreateSqliteContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<PropNestDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new PropNestDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var seller = new ApplicationUser("seller@propnest.vn", "Seller Test", "0909123456", "Seller")
        {
            Id = 1,
            NormalizedEmail = "SELLER@PROPNEST.VN",
            NormalizedUserName = "SELLER@PROPNEST.VN"
        };
        context.Users.Add(seller);
        await context.SaveChangesAsync();

        return context;
    }

    private static async Task<Listing> SeedPendingListingAsync(PropNestDbContext dbContext)
    {
        var listing = new Listing(
            ownerUserId: 1,
            title: "Căn hộ cao cấp Quận 7",
            description: "Căn hộ 2PN diện tích 75m2 đầy đủ tiện ích nội khu.",
            propertyType: "Apartment",
            listingType: "Sale",
            price: 4_800_000_000m,
            area: 75m,
            city: "TP. Hồ Chí Minh",
            district: "Quận 7",
            ward: "Tân Phú",
            address: "1 Nguyễn Văn Linh");

        listing.SubmitForModeration();
        dbContext.Listings.Add(listing);
        await dbContext.SaveChangesAsync();
        return listing;
    }

    private static Mock<ConsumeContext<ListingSubmitted>> CreateConsumeContext(Listing listing, Guid correlationId)
    {
        var message = new ListingSubmitted(
            CorrelationId: correlationId,
            ListingId: listing.ListingId,
            OwnerUserId: listing.OwnerUserId,
            Title: listing.Title,
            Description: listing.Description,
            PropertyType: listing.PropertyType,
            ListingType: listing.ListingType,
            Price: listing.Price,
            Area: listing.Area,
            City: listing.City,
            District: listing.District,
            Ward: listing.Ward,
            Address: listing.Address,
            SubmittedAt: DateTimeOffset.UtcNow);

        var mock = new Mock<ConsumeContext<ListingSubmitted>>();
        mock.SetupGet(x => x.Message).Returns(message);
        mock.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return mock;
    }

    private static AmeModerationRequest CreateSampleAmeRequest(Guid correlationId) => new(
        ListingId: 100,
        CorrelationId: correlationId,
        Title: "Căn hộ Quận 7",
        Description: "Mô tả chi tiết căn hộ.",
        PropertyType: "Apartment",
        ListingType: "Sale",
        Price: 4_500_000_000m,
        Area: 75m,
        City: "TP. Hồ Chí Minh",
        District: "Quận 7",
        Ward: "Tân Phú",
        Address: "1 Nguyễn Văn Linh",
        ActiveRules:
        [
            new AmeContentRuleDto("RULE_1", "BadWord", "lừa đảo", true, "Reject", 100m)
        ],
        PriceBenchmark: new AmePriceBenchmarkDto(
            "Apartment",
            "Sale",
            "TP. Hồ Chí Minh",
            "Quận 7",
            35_000_000m,
            65_000_000m,
            130_000_000m));

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request, cancellationToken);
    }
}
