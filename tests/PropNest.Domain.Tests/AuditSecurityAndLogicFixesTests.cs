using System.Text.Json;
using MassTransit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;
using PropNest.Application.Wallets;
using PropNest.Application.Workflows;
using PropNest.Domain.Listings;
using PropNest.Domain.Users;
using PropNest.Domain.Wallets;
using PropNest.Domain.Workflows;
using PropNest.Infrastructure.Consumers;
using PropNest.Infrastructure.Persistence;
using PropNest.Infrastructure.Persistence.Repositories;
using PropNest.Infrastructure.Services;

namespace PropNest.Domain.Tests;

public sealed class AuditSecurityAndLogicFixesTests
{
    [Fact]
    public async Task WalletRefundShouldRestoreExactPromoAndMainBalancesPreventingPromoLaundering()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var wallet = new Wallet(1);
        wallet.CreditMain(100_000m);
        wallet.CreditPromo(60_000m);
        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync();

        var walletRepo = new WalletRepository(dbContext);
        var correlationId = Guid.NewGuid();
        WalletDeducted? publishedDeducted = null;

        var deductContextMock = new Mock<ConsumeContext<DeductWalletCommand>>();
        deductContextMock.Setup(c => c.Message).Returns(new DeductWalletCommand(correlationId, 1, 100_000m));
        deductContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        deductContextMock
            .Setup(c => c.Publish(It.IsAny<WalletDeducted>(), It.IsAny<CancellationToken>()))
            .Callback<WalletDeducted, CancellationToken>((ev, _) => publishedDeducted = ev)
            .Returns(Task.CompletedTask);

        var deductConsumer = new DeductWalletConsumer(
            dbContext, walletRepo, dbContext, NullLogger<DeductWalletConsumer>.Instance);
        await deductConsumer.Consume(deductContextMock.Object);

        Assert.NotNull(publishedDeducted);
        Assert.Equal(60_000m, publishedDeducted!.PromoUsed);
        Assert.Equal(40_000m, publishedDeducted.MainUsed);

        var afterCharge = await dbContext.Wallets.AsNoTracking().SingleAsync(w => w.UserId == 1);
        Assert.Equal(0m, afterCharge.PromoBalance);
        Assert.Equal(60_000m, afterCharge.MainBalance);

        var refundContextMock = new Mock<ConsumeContext<RefundWalletCommand>>();
        refundContextMock.Setup(c => c.Message).Returns(
            new RefundWalletCommand(correlationId, 1, 100_000m, publishedDeducted.PromoUsed, publishedDeducted.MainUsed));
        refundContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        refundContextMock
            .Setup(c => c.Publish(It.IsAny<WalletRefunded>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var refundConsumer = new RefundWalletConsumer(
            dbContext, walletRepo, dbContext, NullLogger<RefundWalletConsumer>.Instance);
        await refundConsumer.Consume(refundContextMock.Object);

        var afterRefund = await dbContext.Wallets.AsNoTracking().SingleAsync(w => w.UserId == 1);
        Assert.Equal(60_000m, afterRefund.PromoBalance);
        Assert.Equal(100_000m, afterRefund.MainBalance);
    }

    [Fact]
    public async Task WalletTopUpShouldBeIdempotentByCorrelationIdAndRecordTotalBalanceInLedger()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var wallet = new Wallet(1);
        wallet.CreditPromo(50_000m);
        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync();

        var service = new WalletService(new WalletRepository(dbContext), dbContext);
        var correlationId = Guid.NewGuid();

        var first = await service.TopUpAsync(new TopUpWalletCommand(1, 200_000m, "Nạp thử", correlationId));
        var second = await service.TopUpAsync(new TopUpWalletCommand(1, 200_000m, "Nạp thử retry", correlationId));

        Assert.Equal(200_000m, first.MainBalance);
        Assert.Equal(200_000m, second.MainBalance);
        Assert.Equal(250_000m, second.TotalBalance);

        var tx = Assert.Single(await dbContext.WalletTransactions.AsNoTracking().ToListAsync());
        Assert.Equal(50_000m, tx.BalanceBefore);
        Assert.Equal(250_000m, tx.BalanceAfter);
    }

    [Fact]
    public void ListingShouldBlockContentUpdateWhilePendingModeration()
    {
        var listing = CreateSampleListing();
        listing.SubmitForModeration();

        Assert.Throws<InvalidOperationException>(() =>
            listing.UpdateContent(
                "Tiêu đề sửa trái phép",
                "Mô tả sửa trái phép khi đang chờ duyệt.",
                "Apartment",
                "Sale",
                4_000_000_000m,
                70m,
                "TP. Hồ Chí Minh",
                "Quận 7",
                "Phường Tân Phú",
                "10 Nguyễn Văn Linh"));
    }

    [Fact]
    public async Task UploadAndDeleteImagesOnPublishedListingShouldTriggerReModerationAndPersistCoverFlags()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var listing = CreateSampleListing();
        listing.SubmitForModeration();
        listing.Approve(DateTimeOffset.UtcNow);
        listing.ApplyPackagePurchase("Standard", 7, DateTimeOffset.UtcNow);
        dbContext.Listings.Add(listing);
        await dbContext.SaveChangesAsync();

        var listingRepo = new ListingRepository(dbContext);
        var uploadCounter = 0;
        var photoServiceMock = new Mock<IPhotoService>();
        photoServiceMock
            .Setup(p => p.UploadPhotoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                uploadCounter++;
                return new PhotoUploadResult($"https://res.cloudinary.com/demo/img-{uploadCounter}.jpg", $"pub-{uploadCounter}");
            });
        photoServiceMock
            .Setup(p => p.DeletePhotoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var imageService = new ListingImageService(listingRepo, photoServiceMock.Object, dbContext);

        // Upload 2 ảnh lên tin Published -> phải reset sang PendingModeration + bắn Outbox ListingSubmitted
        await using var s1 = new MemoryStream([1, 2, 3]);
        await using var s2 = new MemoryStream([4, 5, 6]);
        var uploaded = await imageService.UploadImagesAsync(
            listing.ListingId,
            1,
            [new UploadImageItem(s1, "a.jpg", "image/jpeg"), new UploadImageItem(s2, "b.jpg", "image/jpeg")]);

        Assert.Equal(2, uploaded.Count);
        Assert.True(uploaded[0].IsCover);
        Assert.False(uploaded[1].IsCover);

        var reloaded = await dbContext.Listings.AsNoTracking().SingleAsync(l => l.ListingId == listing.ListingId);
        Assert.Equal(ListingStatus.PendingModeration, reloaded.Status);
        Assert.Equal(ModerationDecision.Pending, reloaded.ModerationDecision);
        Assert.Single(await dbContext.OutboxMessages.Where(o => o.Type == "ListingSubmitted").ToListAsync());

        // Đang PendingModeration -> phải chặn sửa ảnh
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            imageService.UpdateImageAsync(listing.ListingId, uploaded[1].ImageId, 1, new UpdateListingImageRequest(null, true, null)));

        // Duyệt lại tin -> đổi ảnh bìa sang ảnh #2 phải lưu chuẩn vào DB (fix bug AsNoTracking)
        var trackedListing = await dbContext.Listings.SingleAsync(l => l.ListingId == listing.ListingId);
        trackedListing.Approve(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync();

        await imageService.UpdateImageAsync(listing.ListingId, uploaded[1].ImageId, 1, new UpdateListingImageRequest(null, true, null));
        var afterSwitch = await dbContext.ListingImages.AsNoTracking().OrderBy(i => i.SortOrder).ToListAsync();
        Assert.False(afterSwitch[0].IsCover);
        Assert.True(afterSwitch[1].IsCover);

        // Xóa ảnh bìa (#2) trên tin Published -> ảnh #1 tự lên làm ảnh bìa (lưu DB) + tin quay lại PendingModeration + bắn Outbox
        await imageService.DeleteImageAsync(listing.ListingId, uploaded[1].ImageId, 1);

        var remaining = Assert.Single(await dbContext.ListingImages.AsNoTracking().ToListAsync());
        Assert.True(remaining.IsCover);

        var afterDelete = await dbContext.Listings.AsNoTracking().SingleAsync(l => l.ListingId == listing.ListingId);
        Assert.Equal(ListingStatus.PendingModeration, afterDelete.Status);
        Assert.Equal(2, await dbContext.OutboxMessages.CountAsync(o => o.Type == "ListingSubmitted"));
    }

    [Fact]
    public async Task WorkflowServiceShouldBlockConcurrentListingWorkflowAndRejectExpiredIdempotencyKey()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var listing = CreateSampleListing();
        dbContext.Listings.Add(listing);
        await dbContext.SaveChangesAsync();

        var repo = new WorkflowRepository(dbContext);
        var service = new WorkflowService(repo, dbContext);
        var hash = WorkflowService.ComputeRequestHash("payload-1");

        // 1. Bắt đầu workflow hợp lệ
        var wf1 = await service.StartPurchaseAsync(new PurchasePackageCommand(
            listing.ListingId, 1, "VIP_7", 140_000m, "key-1", "/api/v1/listings/1/purchase-package", hash));
        Assert.Equal(WorkflowStatus.Started, wf1.Status);

        // 2. Bắn thêm request khác Idempotency-Key cho cùng tin khi workflow 1 chưa xong -> bị chặn
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartPurchaseAsync(new PurchasePackageCommand(
                listing.ListingId, 1, "VIP_7", 140_000m, "key-2", "/api/v1/listings/1/purchase-package", hash)));

        // 3. Replay bằng Idempotency-Key đã hết hạn -> bị từ chối
        var expiredCorrelation = Guid.NewGuid();
        dbContext.WorkflowInstances.Add(new WorkflowInstance(expiredCorrelation, listing.ListingId, 1, "STD_7", 50_000m));
        dbContext.IdempotencyRequests.Add(new IdempotencyRequest(
            "expired-key", 1, "/api/v1/listings/1/purchase-package", hash, expiredCorrelation, DateTimeOffset.UtcNow.AddMinutes(-5)));
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartPurchaseAsync(new PurchasePackageCommand(
                listing.ListingId, 1, "STD_7", 50_000m, "expired-key", "/api/v1/listings/1/purchase-package", hash)));
    }

    [Fact]
    public async Task OutboxDispatchShouldQuarantinePoisonPillAndContinueProcessingNewerMessages()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var poison = new OutboxMessage("ListingSubmitted", "{broken-json");
        for (var i = 0; i < OutboxDispatchService.MaxRetryCount; i++)
        {
            poison.MarkFailed("Permanent parse failure");
        }
        var healthy = new OutboxMessage("ListingExpired", JsonSerializer.Serialize(new { ListingId = 99L, OwnerUserId = 1L, PackageCode = "Standard" }));
        dbContext.OutboxMessages.AddRange(poison, healthy);
        await dbContext.SaveChangesAsync();

        var publisherMock = new Mock<IOutboxMessagePublisher>();
        publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dispatcher = new OutboxDispatchService(dbContext, publisherMock.Object, NullLogger<OutboxDispatchService>.Instance);
        var processed = await dispatcher.DispatchPendingAsync();

        Assert.Equal(1, processed);
        var updatedHealthy = await dbContext.OutboxMessages.AsNoTracking().SingleAsync(x => x.OutboxId == healthy.OutboxId);
        Assert.NotNull(updatedHealthy.ProcessedOn);
        var updatedPoison = await dbContext.OutboxMessages.AsNoTracking().SingleAsync(x => x.OutboxId == poison.OutboxId);
        Assert.Null(updatedPoison.ProcessedOn);
        Assert.Equal(OutboxDispatchService.MaxRetryCount, updatedPoison.RetryCount);
    }

    [Fact]
    public async Task WatchdogShouldEscalateStalePendingListingsToManualReview()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var listing = CreateSampleListing();
        listing.SubmitForModeration();
        dbContext.Listings.Add(listing);
        await dbContext.SaveChangesAsync();

        // Giả lập tin bị treo > 15 phút (UpdatedAt quá khứ)
        dbContext.Entry(listing).Property("UpdatedAt").CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-15);
        await dbContext.SaveChangesAsync();

        var watchdog = new ListingModerationWatchdogService(new ListingRepository(dbContext), dbContext);
        var escalated = await watchdog.EscalateStalePendingListingsAsync(TimeSpan.FromMinutes(10));

        Assert.Equal(1, escalated);
        var updated = await dbContext.Listings.AsNoTracking().SingleAsync(l => l.ListingId == listing.ListingId);
        Assert.Equal(ListingStatus.PendingModeration, updated.Status);
        Assert.Equal(ModerationDecision.ManualReview, updated.ModerationDecision);
    }

    [Fact]
    public async Task PublicListingsShouldExcludeExpiredImmediatelyAndIncludeThumbnailUrl()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var active = CreateSampleListing("Căn hộ còn hạn hiển thị");
        active.SubmitForModeration();
        active.Approve(DateTimeOffset.UtcNow);
        active.ApplyPackagePurchase("Standard", 7, DateTimeOffset.UtcNow);

        var expired = CreateSampleListing("Căn hộ đã hết hạn nhưng job chưa chạy");
        expired.SubmitForModeration();
        expired.Approve(DateTimeOffset.UtcNow.AddDays(-10));
        expired.ApplyPackagePurchase("Standard", 7, DateTimeOffset.UtcNow.AddDays(-10));

        dbContext.Listings.AddRange(active, expired);
        await dbContext.SaveChangesAsync();

        dbContext.ListingImages.Add(new ListingImage(active.ListingId, "https://cdn.example/cover.jpg", "pub-cover", 0, true));
        await dbContext.SaveChangesAsync();

        var repo = new ListingRepository(dbContext);
        var result = await repo.GetPublicListingsAsync(new GetPublicListingsQuery());

        var single = Assert.Single(result.Items);
        Assert.Equal(active.ListingId, single.ListingId);
        Assert.Equal("https://cdn.example/cover.jpg", single.ThumbnailUrl);
    }

    private static Listing CreateSampleListing(string title = "Căn hộ Quận 7 view sông thoáng mát") => new(
        ownerUserId: 1,
        title: title,
        description: "Căn hộ 2 phòng ngủ đầy đủ nội thất, sổ hồng chính chủ tại Quận 7.",
        propertyType: "Apartment",
        listingType: "Sale",
        price: 4_500_000_000m,
        area: 75m,
        city: "TP. Hồ Chí Minh",
        district: "Quận 7",
        ward: "Phường Tân Phú",
        address: "10 Nguyễn Văn Linh");

    private static async Task<PropNestDbContext> CreateSqliteContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<PropNestDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new PropNestDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var user = new ApplicationUser("seller@propnest.vn", "Seller Test", "0901234567", "Seller") { Id = 1 };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return dbContext;
    }
}
