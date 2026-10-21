#pragma warning disable CA1707 // Naming Styles: Identifiers should not contain underscores (Task requires exact test names)

using System.Reflection;
using PropNest.Application.Abstractions;
using PropNest.Application.Common;
using PropNest.Application.Listings;
using PropNest.Domain.Listings;
using PropNest.Domain.Packages;
using PropNest.Domain.Workflows;

namespace PropNest.Domain.Tests;

public sealed class ModerationFlowTests
{
    [Fact]
    public async Task approve_success()
    {
        // Arrange
        const long listingId = 1L;
        const long moderatorUserId = 99L;
        var listing = CreateListingWithStatus(listingId, ListingStatus.PendingModeration);
        var package = new PackageDefinitions(1L, "Standard", "Gói thường", 50_000m, 30, "Standard");

        var repository = new FakeListingRepository
        {
            Listing = listing,
            Package = package
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = new ListingService(repository, unitOfWork);

        // Act
        var result = await service.ApproveAsync(listingId, moderatorUserId);

        // Assert
        Assert.Equal(ListingStatus.Published, result.Status);
        Assert.Equal(ModerationDecision.Approved, result.ModerationDecision);
        Assert.NotNull(result.StartDate);
        Assert.NotNull(result.EndDate);
        Assert.True(result.EndDate > result.StartDate);

        // Kiểm tra ghi nhận Moderation Review
        var review = Assert.Single(repository.Reviews);
        Assert.Equal(listingId, review.ListingId);
        Assert.Equal(ModerationDecision.Approved, review.Decision);
        Assert.Equal(moderatorUserId, review.ReviewerUserId);
        Assert.Equal("Manual", review.Source);
        Assert.Null(review.ReasonsJson);

        // Kiểm tra ghi nhận Listing History
        var history = Assert.Single(repository.Histories);
        Assert.Equal(listingId, history.ListingId);
        Assert.Equal(moderatorUserId, history.ActorUserId);
        Assert.Equal("Approve", history.ActionType);

        // Kiểm tra lưu thay đổi vào cơ sở dữ liệu
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.Published)]
    [InlineData(ListingStatus.Rejected)]
    [InlineData(ListingStatus.Hidden)]
    public async Task approve_wrong_status_fails(ListingStatus invalidStatus)
    {
        // Arrange
        const long listingId = 1L;
        const long moderatorUserId = 99L;
        var listing = CreateListingWithStatus(listingId, invalidStatus);
        var package = new PackageDefinitions(1L, "Standard", "Gói thường", 50_000m, 30, "Standard");

        var repository = new FakeListingRepository
        {
            Listing = listing,
            Package = package
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = new ListingService(repository, unitOfWork);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveAsync(listingId, moderatorUserId));

        Assert.Contains("pending moderation", exception.Message, StringComparison.OrdinalIgnoreCase);

        // Đảm bảo không ghi review và history khi xảy ra lỗi
        Assert.Empty(repository.Reviews);
        Assert.Empty(repository.Histories);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task reject_success()
    {
        // Arrange
        const long listingId = 1L;
        const long moderatorUserId = 99L;
        const string reasonsJson = "{\"reason\":\"Hình ảnh mờ và vi phạm quy định đăng tin\"}";
        var listing = CreateListingWithStatus(listingId, ListingStatus.PendingModeration);

        var repository = new FakeListingRepository
        {
            Listing = listing
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = new ListingService(repository, unitOfWork);

        // Act
        var result = await service.RejectAsync(listingId, moderatorUserId, reasonsJson);

        // Assert
        Assert.Equal(ListingStatus.Rejected, result.Status);
        Assert.Equal(ModerationDecision.Rejected, result.ModerationDecision);

        // Kiểm tra ghi nhận Moderation Review
        var review = Assert.Single(repository.Reviews);
        Assert.Equal(listingId, review.ListingId);
        Assert.Equal(ModerationDecision.Rejected, review.Decision);
        Assert.Equal(moderatorUserId, review.ReviewerUserId);
        Assert.Equal("Manual", review.Source);
        Assert.Equal(reasonsJson, review.ReasonsJson);

        // Kiểm tra ghi nhận Listing History
        var history = Assert.Single(repository.Histories);
        Assert.Equal(listingId, history.ListingId);
        Assert.Equal(moderatorUserId, history.ActorUserId);
        Assert.Equal("Reject", history.ActionType);

        // Kiểm tra lưu thay đổi vào cơ sở dữ liệu
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task reject_without_reason_fails(string? invalidReason)
    {
        // Arrange
        const long listingId = 1L;
        const long moderatorUserId = 99L;
        var listing = CreateListingWithStatus(listingId, ListingStatus.PendingModeration);

        var repository = new FakeListingRepository
        {
            Listing = listing
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = new ListingService(repository, unitOfWork);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RejectAsync(listingId, moderatorUserId, invalidReason!));

        Assert.Equal("reasonsJson", exception.ParamName);

        // Đảm bảo không ghi review và history khi thiếu reason
        Assert.Empty(repository.Reviews);
        Assert.Empty(repository.Histories);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task get_pending_moderation_queue_returns_items_with_owner_info()
    {
        // Arrange
        var listingDto = new ListingDto(
            ListingId: 10L,
            OwnerUserId: 1L,
            Title: "Nhà đẹp Quận 1",
            Description: "Mặt tiền thoáng đãng",
            PropertyType: "House",
            ListingType: "Sale",
            Price: 10_000_000_000m,
            Area: 100m,
            City: "Hồ Chí Minh",
            District: "Quận 1",
            Ward: "Bến Nghé",
            Address: "123 Lê Lợi",
            Status: ListingStatus.PendingModeration,
            ModerationDecision: ModerationDecision.NotRequested,
            PackageCode: "Standard",
            StartDate: null,
            EndDate: null,
            RowVersion: [1, 2, 3],
            OwnerName: "Nguyễn Văn A",
            OwnerEmail: "owner@propnest.com",
            OwnerPhoneNumber: "0901234567",
            CreatedAt: DateTimeOffset.UtcNow);

        var repository = new FakeListingRepository();
        repository.PendingListings.Add(listingDto);
        var unitOfWork = new FakeUnitOfWork();
        var service = new ListingService(repository, unitOfWork);

        // Act
        var result = await service.GetPendingModerationAsync();

        // Assert
        Assert.Equal(1, result.Total);
        var item = Assert.Single(result.Items);
        Assert.Equal(10L, item.ListingId);
        Assert.Equal("Nhà đẹp Quận 1", item.Title);
        Assert.Equal("Nguyễn Văn A", item.OwnerName);
        Assert.Equal("owner@propnest.com", item.OwnerEmail);
        Assert.Equal("0901234567", item.OwnerPhoneNumber);
        Assert.Equal(ListingStatus.PendingModeration, item.Status);
    }

    private static Listing CreateListingWithStatus(long listingId, ListingStatus status)
    {
        var listing = new Listing(
            ownerUserId: 10L,
            title: "Căn hộ Vinhomes Grand Park",
            description: "Căn hộ 2 phòng ngủ tiện nghi.",
            propertyType: "Apartment",
            listingType: "Sale",
            price: 3_200_000_000m,
            area: 69m,
            city: "TP Hồ Chí Minh",
            district: "Thủ Đức",
            ward: "Long Thạnh Mỹ",
            address: "Nguyễn Xiển");

        typeof(Listing).GetProperty(nameof(Listing.ListingId), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .SetValue(listing, listingId);

        switch (status)
        {
            case ListingStatus.Draft:
                break;
            case ListingStatus.PendingModeration:
                listing.SubmitForModeration();
                break;
            case ListingStatus.Published:
                listing.SubmitForModeration();
                listing.Approve(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));
                break;
            case ListingStatus.Rejected:
                listing.SubmitForModeration();
                listing.Reject();
                break;
            case ListingStatus.Hidden:
                listing.SubmitForModeration();
                listing.Approve(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));
                listing.Hide();
                break;
            case ListingStatus.Expired:
                listing.SubmitForModeration();
                listing.Approve(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-1));
                listing.Expire(DateTimeOffset.UtcNow);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        return listing;
    }

    private sealed class FakeListingRepository : IListingRepository
    {
        public Listing? Listing { get; set; }
        public PackageDefinitions? Package { get; set; }
        public List<ListingModerationReview> Reviews { get; } = [];
        public List<ListingHistory> Histories { get; } = [];
        public List<OutboxMessage> OutboxMessages { get; } = [];
        public List<ListingDto> PendingListings { get; } = [];

        public Task<ListingOwnerContactDto?> GetOwnerContactAsync(long ownerUserId, CancellationToken cancellationToken = default) => Task.FromResult<ListingOwnerContactDto?>(null);
        public Task<IReadOnlyList<ListingDto>> GetPendingModerationWithOwnersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ListingDto>>(PendingListings);

        public Task<Listing?> GetByIdAsync(long listingId, CancellationToken cancellationToken = default)
            => Task.FromResult(Listing != null && Listing.ListingId == listingId ? Listing : null);

        public Task<PackageDefinitions?> GetPackageByCodeAsync(string packageCode, CancellationToken cancellationToken = default)
            => Task.FromResult(Package != null && Package.PackageCode.Equals(packageCode, StringComparison.OrdinalIgnoreCase) ? Package : null);

        public void AddModerationReview(ListingModerationReview review) => Reviews.Add(review);

        public void AddHistory(ListingHistory history) => Histories.Add(history);

        public void AddOutboxMessage(OutboxMessage message) => OutboxMessages.Add(message);

        public Task<IReadOnlyList<Listing>> GetByOwnerUserIdAsync(long ownerUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Listing>>([]);

        public Task<IReadOnlyList<Listing>> GetPendingModerationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Listing>>([]);

        public Task AddAsync(Listing listing, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void SetOriginalRowVersion(Listing listing, byte[] rowVersion)
        {
        }

        public Task<PagedResult<ListingSummaryDto>> GetPublicListingsAsync(GetPublicListingsQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<ListingSummaryDto>([], 0, 1, 10));

        public Task<IReadOnlyList<ListingHistoryDto>> GetHistoriesByListingIdAsync(long listingId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ListingHistoryDto>>([]);

        public Task<IReadOnlyList<Listing>> GetExpiredPublishedListingsAsync(DateTimeOffset now, int batchSize = 100, CancellationToken cancellationToken = default)
        {
            if (Listing != null && Listing.Status == ListingStatus.Published && Listing.EndDate <= now)
            {
                return Task.FromResult<IReadOnlyList<Listing>>([Listing]);
            }
            return Task.FromResult<IReadOnlyList<Listing>>([]);
        }

        public Task<Listing?> GetByIdWithImagesAsync(long listingId, CancellationToken cancellationToken = default)
            => Task.FromResult<Listing?>(null);

        public Task<IReadOnlyList<ListingImage>> GetImagesByListingIdAsync(long listingId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ListingImage>>([]);

        public Task<ListingImage?> GetImageByIdAsync(long imageId, CancellationToken cancellationToken = default)
            => Task.FromResult<ListingImage?>(null);

        public void AddImage(ListingImage image) { }

        public void RemoveImage(ListingImage image) { }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
    }
}
