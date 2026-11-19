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

        var repository = new FakeListingRepository
        {
            Listing = listing
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = new ListingService(repository, unitOfWork);

        // Act
        var result = await service.ApproveAsync(listingId, moderatorUserId);

        // Assert: first-time approval transitions to PendingPayment until seller pays for a package
        Assert.Equal(ListingStatus.PendingPayment, result.Status);
        Assert.Equal(ModerationDecision.Approved, result.ModerationDecision);
        Assert.Null(result.StartDate);
        Assert.Null(result.EndDate);
        Assert.Null(result.PackageEndDate);

        // Moderation Review recorded
        var review = Assert.Single(repository.Reviews);
        Assert.Equal(listingId, review.ListingId);
        Assert.Equal(ModerationDecision.Approved, review.Decision);
        Assert.Equal(moderatorUserId, review.ReviewerUserId);
        Assert.Equal("Manual", review.Source);
        Assert.Null(review.ReasonsJson);

        // History recorded
        var history = Assert.Single(repository.Histories);
        Assert.Equal(listingId, history.ListingId);
        Assert.Equal(moderatorUserId, history.ActorUserId);
        Assert.Equal("Approve", history.ActionType);

        var outbox = Assert.Single(repository.OutboxMessages);
        Assert.Equal("ListingApprovedPendingPayment", outbox.Type);

        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task approve_edited_active_published_listing_restores_published_without_repayment()
    {
        const long listingId = 2L;
        const long moderatorUserId = 99L;
        var listing = CreateListingWithStatus(listingId, ListingStatus.Published);
        var originalEndDate = listing.EndDate;

        listing.UpdateContent(
            "Updated title for moderation",
            "Updated description content.",
            "Apartment",
            "Sale",
            3_500_000_000m,
            69m,
            "TP Hồ Chí Minh",
            "Thủ Đức",
            "Long Thạnh Mỹ",
            "Nguyễn Xiển");
        Assert.Equal(ListingStatus.PendingModeration, listing.Status);

        var repository = new FakeListingRepository { Listing = listing };
        var unitOfWork = new FakeUnitOfWork();
        var service = new ListingService(repository, unitOfWork);

        var result = await service.ApproveAsync(listingId, moderatorUserId);

        Assert.Equal(ListingStatus.Published, result.Status);
        Assert.Equal(ModerationDecision.Approved, result.ModerationDecision);
        Assert.Equal(originalEndDate, result.EndDate);

        var outbox = Assert.Single(repository.OutboxMessages);
        Assert.Equal("ListingPublished", outbox.Type);
    }

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.Published)]
    [InlineData(ListingStatus.PendingPayment)]
    [InlineData(ListingStatus.Rejected)]
    [InlineData(ListingStatus.Hidden)]
    public async Task approve_wrong_status_fails(ListingStatus invalidStatus)
    {
        // Arrange
        const long listingId = 1L;
        const long moderatorUserId = 99L;
        var listing = CreateListingWithStatus(listingId, invalidStatus);

        var repository = new FakeListingRepository
        {
            Listing = listing
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = new ListingService(repository, unitOfWork);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveAsync(listingId, moderatorUserId));

        Assert.Contains("pending moderation", exception.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Empty(repository.Reviews);
        Assert.Empty(repository.Histories);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task reject_success()
    {
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

        var result = await service.RejectAsync(listingId, moderatorUserId, reasonsJson);

        Assert.Equal(ListingStatus.Rejected, result.Status);
        Assert.Equal(ModerationDecision.Rejected, result.ModerationDecision);

        var review = Assert.Single(repository.Reviews);
        Assert.Equal(listingId, review.ListingId);
        Assert.Equal(ModerationDecision.Rejected, review.Decision);
        Assert.Equal(moderatorUserId, review.ReviewerUserId);
        Assert.Equal("Manual", review.Source);
        Assert.Equal(reasonsJson, review.ReasonsJson);

        var history = Assert.Single(repository.Histories);
        Assert.Equal(listingId, history.ListingId);
        Assert.Equal(moderatorUserId, history.ActorUserId);
        Assert.Equal("Reject", history.ActionType);

        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task reject_without_reason_fails(string? invalidReason)
    {
        const long listingId = 1L;
        const long moderatorUserId = 99L;
        var listing = CreateListingWithStatus(listingId, ListingStatus.PendingModeration);

        var repository = new FakeListingRepository
        {
            Listing = listing
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = new ListingService(repository, unitOfWork);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RejectAsync(listingId, moderatorUserId, invalidReason!));

        Assert.Equal("reasonsJson", exception.ParamName);
        Assert.Empty(repository.Reviews);
        Assert.Empty(repository.Histories);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task get_pending_moderation_queue_returns_items_with_owner_info()
    {
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
            ModerationDecision: ModerationDecision.ManualReview,
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

        var result = await service.GetPendingModerationAsync();

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

        var now = DateTimeOffset.UtcNow;
        switch (status)
        {
            case ListingStatus.Draft:
                break;
            case ListingStatus.PendingModeration:
                listing.SubmitForModeration();
                break;
            case ListingStatus.PendingPayment:
                listing.SubmitForModeration();
                listing.Approve(now);
                break;
            case ListingStatus.Published:
                listing.SubmitForModeration();
                listing.Approve(now);
                listing.ApplyPackagePurchase("Standard", 15, now);
                break;
            case ListingStatus.Rejected:
                listing.SubmitForModeration();
                listing.Reject();
                break;
            case ListingStatus.Hidden:
                listing.SubmitForModeration();
                listing.Approve(now);
                listing.ApplyPackagePurchase("Standard", 15, now);
                listing.Hide();
                break;
            case ListingStatus.Expired:
                listing.SubmitForModeration();
                listing.Approve(now.AddDays(-20));
                listing.ApplyPackagePurchase("Standard", 7, now.AddDays(-20));
                listing.Expire(now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        return listing;
    }

    [Fact]
    public async Task hide_and_unhide_restores_previous_status_from_hide_snapshot()
    {
        const long listingId = 55L;
        var listing = CreateListingWithStatus(listingId, ListingStatus.Published);
        var ownerUserId = listing.OwnerUserId;

        var repository = new FakeListingRepository { Listing = listing };
        var unitOfWork = new FakeUnitOfWork();
        var service = new ListingService(repository, unitOfWork);

        await service.HideAsync(listingId, ownerUserId);
        Assert.Equal(ListingStatus.Hidden, listing.Status);
        var hideHistory = Assert.Single(repository.Histories);
        Assert.Equal("Hide", hideHistory.ActionType);
        Assert.Contains("Published", hideHistory.SnapshotJson, StringComparison.Ordinal);

        var restored = await service.UnhideAsync(listingId, ownerUserId);

        Assert.Equal(ListingStatus.Published, restored.Status);
        Assert.Equal(ListingStatus.Published, listing.Status);
        Assert.Equal(2, repository.Histories.Count);
        Assert.Equal("Unhide", repository.Histories[1].ActionType);
    }

    [Fact]
    public async Task unhide_legacy_listing_without_hide_snapshot_infers_status_from_history()
    {
        const long listingId = 56L;
        var listing = CreateListingWithStatus(listingId, ListingStatus.Hidden);
        var ownerUserId = listing.OwnerUserId;

        var repository = new FakeListingRepository { Listing = listing };
        repository.Histories.Add(ListingHistory.CreateSnapshot(listingId, ownerUserId, "Create", "{}"));
        repository.Histories.Add(ListingHistory.CreateEvent(listingId, ownerUserId, "Submit"));
        repository.Histories.Add(ListingHistory.CreateEvent(listingId, 99L, "Approve"));
        repository.Histories.Add(ListingHistory.CreateEvent(listingId, ownerUserId, "Hide"));

        var unitOfWork = new FakeUnitOfWork();
        var service = new ListingService(repository, unitOfWork);

        var restored = await service.UnhideAsync(listingId, ownerUserId);

        Assert.Equal(ListingStatus.Published, restored.Status);
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

        public Task<IReadOnlyList<PackageDefinitions>> GetActivePackagesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PackageDefinitions>>(Package is not null ? [Package] : []);

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
            => Task.FromResult<IReadOnlyList<ListingHistoryDto>>(Histories
                .Where(h => h.ListingId == listingId)
                .OrderBy(h => h.ActionDate)
                .Select(h => new ListingHistoryDto(h.ActionType, h.ActionDate, h.ActorUserId, "Owner", "Seller", h.SnapshotJson, h.DeltasJson, h.Note))
                .ToList());

        public Task<IReadOnlyList<Listing>> GetExpiredPublishedListingsAsync(DateTimeOffset now, int batchSize = 100, CancellationToken cancellationToken = default)
        {
            if (Listing != null && Listing.Status == ListingStatus.Published && Listing.EndDate <= now)
            {
                return Task.FromResult<IReadOnlyList<Listing>>([Listing]);
            }
            return Task.FromResult<IReadOnlyList<Listing>>([]);
        }

        public Task<IReadOnlyList<Listing>> GetListingsWithExpiredPackageAsync(DateTimeOffset now, int batchSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Listing>>([]);

        public Task<Listing?> GetByIdWithImagesAsync(long listingId, CancellationToken cancellationToken = default)
            => Task.FromResult<Listing?>(null);

        public Task<IReadOnlyList<ListingImage>> GetImagesByListingIdAsync(long listingId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ListingImage>>([]);

        public Task<ListingImage?> GetImageByIdAsync(long imageId, CancellationToken cancellationToken = default)
            => Task.FromResult<ListingImage?>(null);

        public void AddImage(ListingImage image) { }

        public void RemoveImage(ListingImage image) { }

        public Task<AmeModerationResult?> GetLatestAmeResultAsync(long listingId, CancellationToken cancellationToken = default)
            => Task.FromResult<AmeModerationResult?>(null);

        public Task<string?> GetLatestRejectReasonAsync(long listingId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<string?> GetLatestModerationWarningAsync(long listingId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
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
