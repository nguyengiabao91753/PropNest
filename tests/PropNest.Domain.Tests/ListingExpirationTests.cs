#pragma warning disable CA1707

using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PropNest.Application.Abstractions;
using PropNest.Application.Common;
using PropNest.Application.Listings;
using PropNest.Domain.Listings;
using PropNest.Domain.Packages;
using PropNest.Domain.Workflows;
using PropNest.Workflow.Worker.Jobs;
using Quartz;

namespace PropNest.Domain.Tests;

public sealed class ListingExpirationTests
{
    [Fact]
    public async Task ExpireListingsAsync_WhenPublishedListingPastEndDate_ShouldSetStatusExpired_AddHistory_AndAddOutbox()
    {
        // Arrange
        var past = DateTimeOffset.UtcNow.AddDays(-20);
        var listing = CreateListingWithId(101);
        listing.SubmitForModeration();
        listing.Approve(past);
        listing.ApplyPackagePurchase("Standard", 7, past); // Expired already relative to UtcNow

        var repository = new MockListingRepository([listing]);
        var unitOfWork = new MockUnitOfWork();
        var service = new ListingExpirationService(repository, unitOfWork);

        // Act
        var processedCount = await service.ExpireListingsAsync();

        // Assert
        Assert.Equal(1, processedCount);
        Assert.Equal(ListingStatus.Expired, listing.Status);

        var history = Assert.Single(repository.Histories);
        Assert.Equal(101, history.ListingId);
        Assert.Null(history.ActorUserId);
        Assert.Equal("Expire", history.ActionType);

        var outbox = Assert.Single(repository.OutboxMessages);
        Assert.Equal("ListingExpired", outbox.Type);
        using var jsonDoc = JsonDocument.Parse(outbox.PayloadJson);
        Assert.Equal(101, jsonDoc.RootElement.GetProperty("ListingId").GetInt64());
        Assert.Equal(listing.OwnerUserId, jsonDoc.RootElement.GetProperty("OwnerUserId").GetInt64());

        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ExpireListingsAsync_WhenVipPackageEndedButBaseLifespanRemains_ShouldDowngradeToStandard()
    {
        // Arrange: base lifespan 15d starting 10 days ago (still valid for 5 more days), VIP 7d ended 3 days ago
        var start = DateTimeOffset.UtcNow.AddDays(-10);
        var listing = CreateListingWithId(102);
        listing.SubmitForModeration();
        listing.Approve(start);
        listing.ApplyPackagePurchase("Standard", 15, start);
        listing.ApplyPackagePurchase("VIP", 7, start); // VIP expired at start + 6d < UtcNow, base EndDate still > UtcNow

        var repository = new MockListingRepository([], [listing]);
        var unitOfWork = new MockUnitOfWork();
        var service = new ListingExpirationService(repository, unitOfWork);

        // Act
        var expiredCount = await service.ExpireListingsAsync();

        // Assert
        Assert.Equal(0, expiredCount);
        Assert.Equal(ListingStatus.Published, listing.Status);
        Assert.Equal("Standard", listing.PackageCode);
        Assert.Null(listing.PackageEndDate);
        var history = Assert.Single(repository.Histories);
        Assert.Equal("DowngradePackage", history.ActionType);
        var outbox = Assert.Single(repository.OutboxMessages);
        Assert.Equal("ListingPackageDowngraded", outbox.Type);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ExpireListingsAsync_WhenNoCandidates_ShouldDoNothing()
    {
        var repository = new MockListingRepository([]);
        var unitOfWork = new MockUnitOfWork();
        var service = new ListingExpirationService(repository, unitOfWork);

        var processedCount = await service.ExpireListingsAsync();

        Assert.Equal(0, processedCount);
        Assert.Empty(repository.Histories);
        Assert.Empty(repository.OutboxMessages);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ExpireListingsAsync_ProcessesMultipleBatches()
    {
        var past = DateTimeOffset.UtcNow.AddDays(-20);
        var batch1 = Enumerable.Range(1, 100).Select(i =>
        {
            var l = CreateListingWithId(i);
            l.SubmitForModeration();
            l.Approve(past);
            l.ApplyPackagePurchase("Standard", 7, past);
            return l;
        }).ToList();

        var batch2 = Enumerable.Range(101, 20).Select(i =>
        {
            var l = CreateListingWithId(i);
            l.SubmitForModeration();
            l.Approve(past);
            l.ApplyPackagePurchase("Standard", 7, past);
            return l;
        }).ToList();

        var repository = new BatchingMockListingRepository([batch1, batch2]);
        var unitOfWork = new MockUnitOfWork();
        var service = new ListingExpirationService(repository, unitOfWork);

        var processedCount = await service.ExpireListingsAsync();

        Assert.Equal(120, processedCount);
        Assert.Equal(120, repository.Histories.Count);
        Assert.Equal(120, repository.OutboxMessages.Count);
        Assert.Equal(2, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ListingExpirationJob_ExecutesExpirationServiceThroughScope()
    {
        var mockExpirationService = new Mock<IListingExpirationService>();
        mockExpirationService
            .Setup(s => s.ExpireListingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var services = new ServiceCollection();
        services.AddScoped(_ => mockExpirationService.Object);
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var job = new ListingExpirationJob(scopeFactory, NullLogger<ListingExpirationJob>.Instance);

        var mockJobExecutionContext = new Mock<IJobExecutionContext>();
        mockJobExecutionContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        await job.Execute(mockJobExecutionContext.Object);

        mockExpirationService.Verify(s => s.ExpireListingsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void QuartzCronExpression_Every5Minutes_IsCalculatedCorrectly()
    {
        var cronSchedule = new CronExpression("0 0/5 * * * ?");
        var baseTime = new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

        var nextFireTime = cronSchedule.GetNextValidTimeAfter(baseTime);
        var secondFireTime = nextFireTime.HasValue ? cronSchedule.GetNextValidTimeAfter(nextFireTime.Value) : null;

        Assert.NotNull(nextFireTime);
        Assert.Equal(new DateTimeOffset(2026, 10, 13, 12, 5, 0, TimeSpan.Zero), nextFireTime.Value);

        Assert.NotNull(secondFireTime);
        Assert.Equal(new DateTimeOffset(2026, 10, 13, 12, 10, 0, TimeSpan.Zero), secondFireTime.Value);
    }

    private static Listing CreateListingWithId(long listingId)
    {
        var listing = new Listing(
            1,
            $"Listing {listingId}",
            "Description",
            "Apartment",
            "Sale",
            2_000_000_000m,
            60m,
            "Ho Chi Minh City",
            "District 1",
            "Ben Nghe",
            "1 Le Duan");

        var propertyInfo = typeof(Listing).GetProperty(nameof(Listing.ListingId), BindingFlags.Public | BindingFlags.Instance);
        propertyInfo?.SetValue(listing, listingId);
        return listing;
    }

    private sealed class MockListingRepository(List<Listing> expireCandidates, List<Listing>? downgradeCandidates = null) : IListingRepository
    {
        private readonly List<Listing> _downgradeCandidates = downgradeCandidates ?? [];
        public List<ListingHistory> Histories { get; } = [];
        public List<OutboxMessage> OutboxMessages { get; } = [];

        public Task<IReadOnlyList<Listing>> GetExpiredPublishedListingsAsync(DateTimeOffset now, int batchSize = 100, CancellationToken cancellationToken = default)
        {
            var result = expireCandidates.Take(batchSize).ToList();
            expireCandidates.RemoveAll(result.Contains);
            return Task.FromResult<IReadOnlyList<Listing>>(result);
        }

        public Task<IReadOnlyList<Listing>> GetListingsWithExpiredPackageAsync(DateTimeOffset now, int batchSize = 100, CancellationToken cancellationToken = default)
        {
            var result = _downgradeCandidates.Take(batchSize).ToList();
            _downgradeCandidates.RemoveAll(result.Contains);
            return Task.FromResult<IReadOnlyList<Listing>>(result);
        }

        public void AddHistory(ListingHistory history) => Histories.Add(history);
        public void AddOutboxMessage(OutboxMessage message) => OutboxMessages.Add(message);

        public Task<Listing?> GetByIdAsync(long listingId, CancellationToken cancellationToken = default) => Task.FromResult<Listing?>(null);
        public Task<ListingOwnerContactDto?> GetOwnerContactAsync(long ownerUserId, CancellationToken cancellationToken = default) => Task.FromResult<ListingOwnerContactDto?>(null);
        public Task<IReadOnlyList<Listing>> GetByOwnerUserIdAsync(long ownerUserId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Listing>>([]);
        public Task<IReadOnlyList<Listing>> GetPendingModerationAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Listing>>([]);
        public Task<IReadOnlyList<ListingDto>> GetPendingModerationWithOwnersAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ListingDto>>([]);
        public Task AddAsync(Listing listing, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void SetOriginalRowVersion(Listing listing, byte[] rowVersion) { }
        public Task<PagedResult<ListingSummaryDto>> GetPublicListingsAsync(GetPublicListingsQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<ListingSummaryDto>([], 0, 1, 10));
        public Task<IReadOnlyList<ListingHistoryDto>> GetHistoriesByListingIdAsync(long listingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ListingHistoryDto>>([]);
        public Task<PackageDefinitions?> GetPackageByCodeAsync(string packageCode, CancellationToken cancellationToken = default) => Task.FromResult<PackageDefinitions?>(null);
        public Task<IReadOnlyList<PackageDefinitions>> GetActivePackagesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PackageDefinitions>>([]);
        public void AddModerationReview(ListingModerationReview review) { }
        public Task<Listing?> GetByIdWithImagesAsync(long listingId, CancellationToken cancellationToken = default) => Task.FromResult<Listing?>(null);
        public Task<IReadOnlyList<ListingImage>> GetImagesByListingIdAsync(long listingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ListingImage>>([]);
        public Task<ListingImage?> GetImageByIdAsync(long imageId, CancellationToken cancellationToken = default) => Task.FromResult<ListingImage?>(null);
        public void AddImage(ListingImage image) { }
        public void RemoveImage(ListingImage image) { }
        public Task<AmeModerationResult?> GetLatestAmeResultAsync(long listingId, CancellationToken cancellationToken = default) => Task.FromResult<AmeModerationResult?>(null);
        public Task<string?> GetLatestRejectReasonAsync(long listingId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private sealed class BatchingMockListingRepository(List<List<Listing>> batches) : IListingRepository
    {
        private int _currentBatchIndex;
        public List<ListingHistory> Histories { get; } = [];
        public List<OutboxMessage> OutboxMessages { get; } = [];

        public Task<IReadOnlyList<Listing>> GetExpiredPublishedListingsAsync(DateTimeOffset now, int batchSize = 100, CancellationToken cancellationToken = default)
        {
            if (_currentBatchIndex < batches.Count)
            {
                var batch = batches[_currentBatchIndex++];
                return Task.FromResult<IReadOnlyList<Listing>>(batch);
            }
            return Task.FromResult<IReadOnlyList<Listing>>([]);
        }

        public Task<IReadOnlyList<Listing>> GetListingsWithExpiredPackageAsync(DateTimeOffset now, int batchSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Listing>>([]);

        public void AddHistory(ListingHistory history) => Histories.Add(history);
        public void AddOutboxMessage(OutboxMessage message) => OutboxMessages.Add(message);

        public Task<Listing?> GetByIdAsync(long listingId, CancellationToken cancellationToken = default) => Task.FromResult<Listing?>(null);
        public Task<ListingOwnerContactDto?> GetOwnerContactAsync(long ownerUserId, CancellationToken cancellationToken = default) => Task.FromResult<ListingOwnerContactDto?>(null);
        public Task<IReadOnlyList<Listing>> GetByOwnerUserIdAsync(long ownerUserId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Listing>>([]);
        public Task<IReadOnlyList<Listing>> GetPendingModerationAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Listing>>([]);
        public Task<IReadOnlyList<ListingDto>> GetPendingModerationWithOwnersAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ListingDto>>([]);
        public Task AddAsync(Listing listing, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void SetOriginalRowVersion(Listing listing, byte[] rowVersion) { }
        public Task<PagedResult<ListingSummaryDto>> GetPublicListingsAsync(GetPublicListingsQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<ListingSummaryDto>([], 0, 1, 10));
        public Task<IReadOnlyList<ListingHistoryDto>> GetHistoriesByListingIdAsync(long listingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ListingHistoryDto>>([]);
        public Task<PackageDefinitions?> GetPackageByCodeAsync(string packageCode, CancellationToken cancellationToken = default) => Task.FromResult<PackageDefinitions?>(null);
        public Task<IReadOnlyList<PackageDefinitions>> GetActivePackagesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PackageDefinitions>>([]);
        public void AddModerationReview(ListingModerationReview review) { }
        public Task<Listing?> GetByIdWithImagesAsync(long listingId, CancellationToken cancellationToken = default) => Task.FromResult<Listing?>(null);
        public Task<IReadOnlyList<ListingImage>> GetImagesByListingIdAsync(long listingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ListingImage>>([]);
        public Task<ListingImage?> GetImageByIdAsync(long imageId, CancellationToken cancellationToken = default) => Task.FromResult<ListingImage?>(null);
        public void AddImage(ListingImage image) { }
        public void RemoveImage(ListingImage image) { }
        public Task<AmeModerationResult?> GetLatestAmeResultAsync(long listingId, CancellationToken cancellationToken = default) => Task.FromResult<AmeModerationResult?>(null);
        public Task<string?> GetLatestRejectReasonAsync(long listingId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private sealed class MockUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
    }
}
