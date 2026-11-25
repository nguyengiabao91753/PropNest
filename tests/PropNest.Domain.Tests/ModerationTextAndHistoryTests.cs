using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PropNest.Application.Listings;
using PropNest.Domain.Common;
using PropNest.Domain.Listings;
using PropNest.Domain.Users;
using PropNest.Infrastructure.Persistence;
using PropNest.Infrastructure.Persistence.Repositories;

namespace PropNest.Domain.Tests;

public sealed class ModerationTextAndHistoryTests
{
    [Fact]
    public void ModerationTextFormatterShouldConvertRawJsonObjectToCleanVietnameseText()
    {
        const string rawAmeJson = """
            {
              "Summary": "Ảnh (#1) bị mờ hoặc mất nét (độ nét=12.4 < 85.0) — chuyển Admin kiểm duyệt thủ công.",
              "ReasonCodes": ["IMAGE_BLURRY", "LLM_UNAVAILABLE_FALLBACK_MANUAL"],
              "Violations": [
                {
                  "Rule": "IMAGE_BLURRY",
                  "Severity": "manual_review",
                  "Message": "Ảnh (#1) bị mờ hoặc mất nét (độ nét=12.4 < 85.0) — chuyển Admin kiểm duyệt thủ công."
                }
              ]
            }
            """;

        var formatted = ModerationTextFormatter.FormatModerationReasonText(rawAmeJson);

        Assert.False(string.IsNullOrWhiteSpace(formatted));
        Assert.DoesNotContain("{", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("}", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("IMAGE_BLURRY", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("độ nét=", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Admin", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mờ", formatted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModerationTextFormatterShouldSanitizeFallbackTechnicalErrors()
    {
        var fallback = AmeModerationResponse.FallbackManualReview(
            "AME_UNAVAILABLE_FALLBACK_MANUAL",
            "System.Net.Http.HttpRequestException: No connection could be made (localhost:8001)");

        Assert.Equal(ModerationDecision.ManualReview, fallback.Decision);
        Assert.DoesNotContain("HttpRequestException", fallback.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("localhost", fallback.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AME", fallback.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("kiểm duyệt thủ công", fallback.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModerationTextFormatterShouldHandleTruncatedLegacyJsonSafely()
    {
        const string truncatedJson = "{\"Summary\":\"Nội dung mô tả còn quá ngắn (12 ký tự).\",\"ReasonCodes\":[\"SHORT_DESC...";

        var formatted = ModerationTextFormatter.FormatModerationReasonText(truncatedJson);

        Assert.DoesNotContain("{", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("ReasonCodes", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quá ngắn", formatted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModerationTextFormatterShouldExcludePassOnlyCodesAndMapFailureCodes()
    {
        Assert.True(ModerationTextFormatter.IsPassOnlyCode("PASS_ALL_RULES"));
        Assert.True(ModerationTextFormatter.IsPassOnlyCode("CLEAN_CONTENT"));
        Assert.True(ModerationTextFormatter.IsPassOnlyCode("PRICE_WITHIN_BENCHMARK"));
        Assert.Equal(string.Empty, ModerationTextFormatter.MapCodeToFriendlyMessage("PASS_ALL_RULES"));

        var title = ModerationTextFormatter.GetRuleDisplayTitle("IMAGE_BLURRY");
        var msg = ModerationTextFormatter.MapCodeToFriendlyMessage("LLM_UNAVAILABLE_FALLBACK_MANUAL");
        Assert.DoesNotContain("_", title, StringComparison.Ordinal);
        Assert.DoesNotContain("_", msg, StringComparison.Ordinal);
        Assert.DoesNotContain("LLM", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListingServiceGetHistoriesShouldHideInternalFailuresDeduplicateApprovalsAndMaskApprover()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var listing = CreateSampleListing();
        listing.SubmitForModeration();
        listing.Approve(DateTimeOffset.UtcNow);
        dbContext.Listings.Add(listing);
        await dbContext.SaveChangesAsync();

        var t0 = DateTimeOffset.UtcNow.AddMinutes(-10);
        dbContext.ListingHistories.AddRange(
            ListingHistory.CreateEvent(listing.ListingId, 1L, "Create", "Tạo mới tin đăng"),
            ListingHistory.CreateEvent(listing.ListingId, 1L, "Submit", "Gửi duyệt tin"),
            ListingHistory.CreateEvent(listing.ListingId, null, "RouteManualReview", "AME chuyển sang hàng chờ kiểm duyệt thủ công của Admin."),
            ListingHistory.CreateEvent(listing.ListingId, null, "EscalateManualReview", "Quá thời gian chờ AME xử lý tự động."),
            ListingHistory.CreateEvent(listing.ListingId, null, "AutoApprove", "AME duyệt tự động thành công."),
            ListingHistory.CreateEvent(listing.ListingId, 99L, "Approve", "Admin duyệt tin thành công."));
        await dbContext.SaveChangesAsync();

        // Đảm bảo thứ tự thời gian xác định
        _ = t0;
        var service = new ListingService(new ListingRepository(dbContext), dbContext);
        var histories = await service.GetHistoriesAsync(listing.ListingId);

        Assert.Equal(3, histories.Count);
        Assert.Equal("Create", histories[0].ActionType);
        Assert.Equal("Submit", histories[1].ActionType);

        var approveEntry = histories[2];
        Assert.Equal("Approve", approveEntry.ActionType);
        Assert.Null(approveEntry.ActorUserId);
        Assert.Null(approveEntry.ActorName);
        Assert.Null(approveEntry.ActorRole);
        Assert.Equal("Tin đăng đã được duyệt.", approveEntry.Note);
        Assert.DoesNotContain(histories, h => h.ActionType is "RouteManualReview" or "EscalateManualReview" or "AutoApprove");
    }

    [Fact]
    public async Task WatchdogShouldEscalateStaleListingWithoutCreatingListingHistory()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var listing = CreateSampleListing();
        listing.SubmitForModeration();
        dbContext.Listings.Add(listing);
        await dbContext.SaveChangesAsync();

        var watchdog = new ListingModerationWatchdogService(new ListingRepository(dbContext), dbContext);
        var escalated = await watchdog.EscalateStalePendingListingsAsync(TimeSpan.Zero);

        Assert.Equal(1, escalated);
        var updated = await dbContext.Listings.SingleAsync(x => x.ListingId == listing.ListingId);
        Assert.Equal(ModerationDecision.ManualReview, updated.ModerationDecision);
        Assert.False(await dbContext.ListingHistories.AnyAsync(h => h.ListingId == listing.ListingId));
    }

    [Fact]
    public async Task ListingServiceGetByIdShouldReturnPlainTextRejectReasonWithoutJsonOrCodeTokens()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var listing = CreateSampleListing();
        listing.SubmitForModeration();
        listing.Reject();
        dbContext.Listings.Add(listing);
        await dbContext.SaveChangesAsync();

        const string ameRejectJson = """
            {
              "Summary": "Phát hiện số điện thoại trong nội dung tin đăng",
              "ReasonCodes": ["PHONE_EXPOSURE"],
              "Violations": [
                {
                  "Rule": "PHONE_EXPOSURE",
                  "Severity": "critical",
                  "Message": "Phát hiện số điện thoại trong nội dung tin đăng"
                }
              ]
            }
            """;

        dbContext.ListingModerationReviews.Add(new ListingModerationReview(
            listing.ListingId,
            "AME",
            ModerationDecision.Rejected,
            ameRejectJson,
            reviewerUserId: null));
        await dbContext.SaveChangesAsync();

        var service = new ListingService(new ListingRepository(dbContext), dbContext);
        var dto = await service.GetByIdAsync(listing.ListingId);

        Assert.NotNull(dto);
        Assert.False(string.IsNullOrWhiteSpace(dto!.LatestRejectReason));
        Assert.DoesNotContain("{", dto.LatestRejectReason!, StringComparison.Ordinal);
        Assert.DoesNotContain("PHONE_EXPOSURE", dto.LatestRejectReason!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("số điện thoại", dto.LatestRejectReason!, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<PropNestDbContext> CreateSqliteContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<PropNestDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new PropNestDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Users.AddRange(
            new ApplicationUser("seller@propnest.vn", "Người Bán A", "0909123456", "Seller")
            {
                Id = 1,
                NormalizedEmail = "SELLER@PROPNEST.VN",
                NormalizedUserName = "SELLER@PROPNEST.VN"
            },
            new ApplicationUser("admin@propnest.vn", "Quản Trị Viên", "0909000001", "Admin")
            {
                Id = 99,
                NormalizedEmail = "ADMIN@PROPNEST.VN",
                NormalizedUserName = "ADMIN@PROPNEST.VN"
            });
        await context.SaveChangesAsync();

        return context;
    }

    private static Listing CreateSampleListing() => new(
        ownerUserId: 1,
        title: "Căn hộ 2PN Quận 7 sổ hồng riêng",
        description: "Căn hộ thoáng mát, đầy đủ nội thất cao cấp, tiện ích hồ bơi và công viên.",
        propertyType: "Apartment",
        listingType: "Sale",
        price: 4_200_000_000m,
        area: 72m,
        city: "Thành phố Hồ Chí Minh",
        district: "Quận 7",
        ward: "Phường Tân Phú",
        address: "10 Nguyễn Văn Linh");
}
