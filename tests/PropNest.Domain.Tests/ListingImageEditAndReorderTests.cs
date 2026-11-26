using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;
using PropNest.Domain.Listings;
using PropNest.Domain.Users;
using PropNest.Domain.Workflows;
using PropNest.Infrastructure.Persistence;
using PropNest.Infrastructure.Persistence.Repositories;

namespace PropNest.Domain.Tests;

public sealed class ListingImageEditAndReorderTests
{
    [Fact]
    public async Task SyncImagesOnCreateShouldPersistExactCustomOrderAndSetFirstAsCover()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var listing = CreateSampleListing();
        dbContext.Listings.Add(listing);
        await dbContext.SaveChangesAsync();

        var photoMock = CreateMockPhotoService();
        var imageService = new ListingImageService(new ListingRepository(dbContext), photoMock.Object, dbContext);

        await using var s0 = new MemoryStream([1, 2, 3]);
        await using var s1 = new MemoryStream([4, 5, 6]);
        await using var s2 = new MemoryStream([7, 8, 9]);

        // Người bán đảo thứ tự lúc Create: file #2 làm ảnh bìa, sau đó #0, sau đó #1
        var result = await imageService.SyncImagesAsync(
            listing.ListingId,
            userId: 1,
            orderTokens: ["new:2", "new:0", "new:1"],
            newFiles:
            [
                new UploadImageItem(s0, "first.jpg", "image/jpeg"),
                new UploadImageItem(s1, "second.jpg", "image/jpeg"),
                new UploadImageItem(s2, "third.jpg", "image/jpeg")
            ]);

        Assert.Equal(3, result.Images.Count);
        Assert.EndsWith("third.jpg", result.Images[0].Url);
        Assert.Equal(0, result.Images[0].SortOrder);
        Assert.True(result.Images[0].IsCover);

        Assert.EndsWith("first.jpg", result.Images[1].Url);
        Assert.Equal(1, result.Images[1].SortOrder);
        Assert.False(result.Images[1].IsCover);

        Assert.EndsWith("second.jpg", result.Images[2].Url);
        Assert.Equal(2, result.Images[2].SortOrder);
        Assert.False(result.Images[2].IsCover);

        // Tin Draft vẫn giữ nguyên trạng thái Draft
        var reloaded = await dbContext.Listings.AsNoTracking().SingleAsync(x => x.ListingId == listing.ListingId);
        Assert.Equal(ListingStatus.Draft, reloaded.Status);
    }

    [Fact]
    public async Task EditPublishedListingWithDeferredImageSyncThenContentUpdateShouldEmitSingleModerationEventWithFinalOrderedImages()
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

        var imgA = new ListingImage(listing.ListingId, "https://cdn.test/old-a.jpg", "pub-old-a", 0, true);
        var imgB = new ListingImage(listing.ListingId, "https://cdn.test/old-b.jpg", "pub-old-b", 1, false);
        dbContext.ListingImages.AddRange(imgA, imgB);
        await dbContext.SaveChangesAsync();

        var repo = new ListingRepository(dbContext);
        var photoMock = CreateMockPhotoService();
        var imageService = new ListingImageService(repo, photoMock.Object, dbContext);
        var listingService = new ListingService(repo, dbContext);

        // Bước 1 của Edit: xóa imgA, thêm new-c.jpg và đưa lên vị trí #1 (ảnh bìa), imgB xuống vị trí #2
        await using var sNew = new MemoryStream([10, 20, 30]);
        var syncResult = await imageService.SyncImagesAsync(
            listing.ListingId,
            userId: 1,
            orderTokens: ["new:0", $"existing:{imgB.ImageId}"],
            newFiles: [new UploadImageItem(sNew, "new-c.jpg", "image/jpeg")],
            expectedRowVersion: listing.RowVersion,
            deferReModeration: true);

        Assert.Equal(2, syncResult.Images.Count);
        Assert.EndsWith("new-c.jpg", syncResult.Images[0].Url);
        Assert.True(syncResult.Images[0].IsCover);
        Assert.Equal(0, syncResult.Images[0].SortOrder);
        Assert.Equal("https://cdn.test/old-b.jpg", syncResult.Images[1].Url);
        Assert.False(syncResult.Images[1].IsCover);
        Assert.Equal(1, syncResult.Images[1].SortOrder);

        // Xác nhận ảnh cũ bị xóa đã được dọn trên Cloudinary và tin CHƯA bị khóa PendingModeration trước bước UpdateContent
        photoMock.Verify(p => p.DeletePhotoAsync("pub-old-a", It.IsAny<CancellationToken>()), Times.Once);
        var midState = await dbContext.Listings.AsNoTracking().SingleAsync(x => x.ListingId == listing.ListingId);
        Assert.Equal(ListingStatus.Published, midState.Status);

        // Bước 2 của Edit: gọi ListingService.UpdateAsync với RowVersion mới từ syncResult
        var updatedDto = await listingService.UpdateAsync(new UpdateListingCommand(
            ListingId: listing.ListingId,
            OwnerUserId: 1,
            Title: "Căn hộ Quận 7 đã cập nhật ảnh và nội dung mới",
            Description: "Mô tả mới chi tiết hơn về căn hộ 2 phòng ngủ đầy đủ nội thất cao cấp.",
            PropertyType: "Apartment",
            ListingType: "Sale",
            Price: 4_800_000_000m,
            Area: 75m,
            City: "TP. Hồ Chí Minh",
            District: "Quận 7",
            Ward: "Phường Tân Phú",
            Address: "10 Nguyễn Văn Linh",
            RowVersion: syncResult.RowVersion));

        Assert.Equal(ListingStatus.PendingModeration, updatedDto.Status);
        Assert.Equal(2, updatedDto.Images!.Count);
        Assert.EndsWith("new-c.jpg", updatedDto.Images[0]);
        Assert.Equal("https://cdn.test/old-b.jpg", updatedDto.Images[1]);

        // Chỉ phát sinh DUY NHẤT 1 sự kiện ListingSubmitted chứa đúng thứ tự ảnh mới
        var submittedOutbox = Assert.Single(await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(o => o.Type == "ListingSubmitted")
            .ToListAsync());

        var payload = JsonSerializer.Deserialize<ListingSubmitted>(submittedOutbox.PayloadJson);
        Assert.NotNull(payload);
        Assert.NotNull(payload!.Images);
        Assert.Equal(2, payload.Images!.Count);
        Assert.EndsWith("new-c.jpg", payload.Images[0]);
        Assert.Equal("https://cdn.test/old-b.jpg", payload.Images[1]);
    }

    [Fact]
    public async Task ReorderImagesOnPublishedListingShouldUpdateOrderCoverAndEmitMediaSyncWithoutReModeration()
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

        var i1 = new ListingImage(listing.ListingId, "https://cdn.test/1.jpg", "p1", 0, true);
        var i2 = new ListingImage(listing.ListingId, "https://cdn.test/2.jpg", "p2", 1, false);
        var i3 = new ListingImage(listing.ListingId, "https://cdn.test/3.jpg", "p3", 2, false);
        dbContext.ListingImages.AddRange(i1, i2, i3);
        await dbContext.SaveChangesAsync();

        var imageService = new ListingImageService(new ListingRepository(dbContext), CreateMockPhotoService().Object, dbContext);
        var reordered = await imageService.ReorderImagesAsync(listing.ListingId, 1, [i3.ImageId, i1.ImageId, i2.ImageId]);

        Assert.Equal([i3.ImageId, i1.ImageId, i2.ImageId], reordered.Select(x => x.ImageId).ToArray());
        Assert.True(reordered[0].IsCover);
        Assert.False(reordered[1].IsCover);
        Assert.False(reordered[2].IsCover);
        Assert.Equal([0, 1, 2], reordered.Select(x => x.SortOrder).ToArray());

        // Đổi thứ tự ảnh hiện có KHÔNG đá tin Published quay lại PendingModeration
        var reloaded = await dbContext.Listings.AsNoTracking().SingleAsync(x => x.ListingId == listing.ListingId);
        Assert.Equal(ListingStatus.Published, reloaded.Status);
        Assert.Empty(await dbContext.OutboxMessages.Where(o => o.Type == "ListingSubmitted").ToListAsync());
        Assert.Single(await dbContext.OutboxMessages.Where(o => o.Type == "ListingMediaUpdated").ToListAsync());
    }

    [Fact]
    public async Task SyncImagesShouldRejectStaleRowVersionBeforeUploadingAnyPhoto()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var listing = CreateSampleListing();
        dbContext.Listings.Add(listing);
        await dbContext.SaveChangesAsync();

        var photoMock = CreateMockPhotoService();
        var imageService = new ListingImageService(new ListingRepository(dbContext), photoMock.Object, dbContext);

        await using var s = new MemoryStream([1, 2, 3]);
        var staleRowVersion = new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 };

        await Assert.ThrowsAsync<ListingConcurrencyException>(() =>
            imageService.SyncImagesAsync(
                listing.ListingId,
                1,
                ["new:0"],
                [new UploadImageItem(s, "stale.jpg", "image/jpeg")],
                expectedRowVersion: staleRowVersion,
                deferReModeration: true));

        // Tuyệt đối không upload thừa lên Cloudinary khi phát hiện RowVersion cũ
        photoMock.Verify(
            p => p.UploadPhotoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncImagesShouldAllowEditingImagesOnExpiredListing()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var listing = CreateSampleListing();
        listing.SubmitForModeration();
        listing.Approve(DateTimeOffset.UtcNow.AddDays(-20));
        listing.ApplyPackagePurchase("Standard", 7, DateTimeOffset.UtcNow.AddDays(-20));
        listing.Expire(DateTimeOffset.UtcNow);
        dbContext.Listings.Add(listing);
        await dbContext.SaveChangesAsync();

        Assert.Equal(ListingStatus.Expired, listing.Status);

        var imageService = new ListingImageService(new ListingRepository(dbContext), CreateMockPhotoService().Object, dbContext);
        await using var s = new MemoryStream([1, 2, 3]);
        var synced = await imageService.SyncImagesAsync(
            listing.ListingId,
            1,
            ["new:0"],
            [new UploadImageItem(s, "fresh.jpg", "image/jpeg")],
            deferReModeration: false);

        Assert.Single(synced.Images);
        Assert.True(synced.Images[0].IsCover);

        var reloaded = await dbContext.Listings.AsNoTracking().SingleAsync(x => x.ListingId == listing.ListingId);
        Assert.Equal(ListingStatus.PendingModeration, reloaded.Status);
    }

    private static Mock<IPhotoService> CreateMockPhotoService()
    {
        var counter = 0;
        var mock = new Mock<IPhotoService>();
        mock.Setup(p => p.UploadPhotoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, string fileName, string _, CancellationToken _) =>
            {
                counter++;
                return new PhotoUploadResult($"https://res.cloudinary.com/demo/{counter}-{fileName}", $"pub-{counter}");
            });
        mock.Setup(p => p.DeletePhotoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock;
    }

    private static Listing CreateSampleListing() => new(
        ownerUserId: 1,
        title: "Căn hộ Quận 7 view sông thoáng mát",
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
