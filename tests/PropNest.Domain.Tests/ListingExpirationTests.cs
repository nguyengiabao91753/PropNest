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
        var listing = CreateListingWithId(101);
        listing.SubmitForModeration();
        var now = DateTimeOffset.UtcNow;
        listing.Approve(now.AddDays(-10), now.AddDays(-1)); // EndDate is yesterday

        var repository = new MockListingRepository([listing]);
        var unitOfWork = new MockUnitOfWork();
        var service = new ListingExpirationService(repository, unitOfWork);

        // Act
        var processedCount = await service.ExpireListingsAsync();

        // Assert
        Assert.Equal(1, processedCount);
        Assert.Equal(ListingStatus.Expired, listing.Status);

        // Kiểm tra history
        var history = Assert.Single(repository.Histories);
        Assert.Equal(101, history.ListingId);
        Assert.Null(history.ActorUserId);
        Assert.Equal("Expire", history.ActionType);

        // Kiểm tra outbox message
        var outbox = Assert.Single(repository.OutboxMessages);
        Assert.Equal("ListingExpired", outbox.Type);
        using var jsonDoc = JsonDocument.Parse(outbox.PayloadJson);
        Assert.Equal(101, jsonDoc.RootElement.GetProperty("ListingId").GetInt64());
        Assert.Equal(listing.OwnerUserId, jsonDoc.RootElement.GetProperty("OwnerUserId").GetInt64());

        // Kiểm tra commit transaction
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ExpireListingsAsync_WhenNoCandidates_ShouldDoNothing()
    {
        // Arrange
        var repository = new MockListingRepository([]);
        var unitOfWork = new MockUnitOfWork();
        var service = new ListingExpirationService(repository, unitOfWork);

        // Act
        var processedCount = await service.ExpireListingsAsync();

        // Assert
        Assert.Equal(0, processedCount);
        Assert.Empty(repository.Histories);
        Assert.Empty(repository.OutboxMessages);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ExpireListingsAsync_ProcessesMultipleBatches()
    {
        // Arrange
        var batch1 = Enumerable.Range(1, 100).Select(i =>
        {
            var l = CreateListingWithId(i);
            l.SubmitForModeration();
            l.Approve(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-1));
            return l;
        }).ToList();

        var batch2 = Enumerable.Range(101, 20).Select(i =>
        {
            var l = CreateListingWithId(i);
            l.SubmitForModeration();
            l.Approve(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-1));
            return l;
        }).ToList();

        var repository = new BatchingMockListingRepository([batch1, batch2]);
        var unitOfWork = new MockUnitOfWork();
        var service = new ListingExpirationService(repository, unitOfWork);

        // Act
        var processedCount = await service.ExpireListingsAsync();

        // Assert
        Assert.Equal(120, processedCount);
        Assert.Equal(120, repository.Histories.Count);
        Assert.Equal(120, repository.OutboxMessages.Count);
        Assert.Equal(2, unitOfWork.SaveChangesCalls); // 2 batches saved separately
    }

    [Fact]
    public async Task ListingExpirationJob_ExecutesExpirationServiceThroughScope()
    {
        // Arrange
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

        // Act
        await job.Execute(mockJobExecutionContext.Object);

        // Assert
        mockExpirationService.Verify(s => s.ExpireListingsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void QuartzCronExpression_Every5Minutes_IsCalculatedCorrectly()
    {
        // Arrange
        var cronSchedule = new CronExpression("0 0/5 * * * ?");
        var baseTime = new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

        // Act
        var nextFireTime = cronSchedule.GetNextValidTimeAfter(baseTime);
        var secondFireTime = nextFireTime.HasValue ? cronSchedule.GetNextValidTimeAfter(nextFireTime.Value) : null;

        // Assert
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

    private sealed class MockListingRepository(List<Listing> candidates) : IListingRepository
    {
        public List<ListingHistory> Histories { get; } = [];
        public List<OutboxMessage> OutboxMessages { get; } = [];

        public Task<IReadOnlyList<Listing>> GetExpiredPublishedListingsAsync(DateTimeOffset now, int batchSize = 100, CancellationToken cancellationToken = default)
        {
            var result = candidates.Take(batchSize).ToList();
            candidates.RemoveAll(result.Contains);
            return Task.FromResult<IReadOnlyList<Listing>>(result);
        }

        public void AddHistory(ListingHistory history) => Histories.Add(history);
        public void AddOutboxMessage(OutboxMessage message) => OutboxMessages.Add(message);

        public Task<Listing?> GetByIdAsync(long listingId, CancellationToken cancellationToken = default) => Task.FromResult<Listing?>(null);
        public Task<IReadOnlyList<Listing>> GetByOwnerUserIdAsync(long ownerUserId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Listing>>([]);
        public Task<IReadOnlyList<Listing>> GetPendingModerationAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Listing>>([]);
        public Task<IReadOnlyList<ListingDto>> GetPendingModerationWithOwnersAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ListingDto>>([]);
        public Task AddAsync(Listing listing, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void SetOriginalRowVersion(Listing listing, byte[] rowVersion) { }
        public Task<PagedResult<ListingSummaryDto>> GetPublicListingsAsync(GetPublicListingsQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<ListingSummaryDto>([], 0, 1, 10));
        public Task<IReadOnlyList<ListingHistoryDto>> GetHistoriesByListingIdAsync(long listingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ListingHistoryDto>>([]);
        public Task<PackageDefinitions?> GetPackageByCodeAsync(string packageCode, CancellationToken cancellationToken = default) => Task.FromResult<PackageDefinitions?>(null);
        public void AddModerationReview(ListingModerationReview review) { }
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

        public void AddHistory(ListingHistory history) => Histories.Add(history);
        public void AddOutboxMessage(OutboxMessage message) => OutboxMessages.Add(message);

        public Task<Listing?> GetByIdAsync(long listingId, CancellationToken cancellationToken = default) => Task.FromResult<Listing?>(null);
        public Task<IReadOnlyList<Listing>> GetByOwnerUserIdAsync(long ownerUserId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Listing>>([]);
        public Task<IReadOnlyList<Listing>> GetPendingModerationAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Listing>>([]);
        public Task<IReadOnlyList<ListingDto>> GetPendingModerationWithOwnersAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ListingDto>>([]);
        public Task AddAsync(Listing listing, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void SetOriginalRowVersion(Listing listing, byte[] rowVersion) { }
        public Task<PagedResult<ListingSummaryDto>> GetPublicListingsAsync(GetPublicListingsQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<ListingSummaryDto>([], 0, 1, 10));
        public Task<IReadOnlyList<ListingHistoryDto>> GetHistoriesByListingIdAsync(long listingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ListingHistoryDto>>([]);
        public Task<PackageDefinitions?> GetPackageByCodeAsync(string packageCode, CancellationToken cancellationToken = default) => Task.FromResult<PackageDefinitions?>(null);
        public void AddModerationReview(ListingModerationReview review) { }
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
