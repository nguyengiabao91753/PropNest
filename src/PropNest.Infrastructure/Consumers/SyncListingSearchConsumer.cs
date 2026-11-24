using System.Globalization;
using MassTransit;
using Meilisearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings.Search;
using PropNest.Domain.Listings;
using PropNest.Infrastructure.Persistence;

namespace PropNest.Infrastructure.Consumers;

public sealed partial class SyncListingSearchConsumer : IConsumer<ListingSearchSyncRequested>
{
    private readonly PropNestDbContext _dbContext;
    private readonly MeilisearchClient _meilisearchClient;
    private readonly MeilisearchOptions _options;
    private readonly ILogger<SyncListingSearchConsumer> _logger;

    public SyncListingSearchConsumer(
        PropNestDbContext dbContext,
        MeilisearchClient meilisearchClient,
        IOptions<MeilisearchOptions> options,
        ILogger<SyncListingSearchConsumer> logger)
    {
        _dbContext = dbContext;
        _meilisearchClient = meilisearchClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ListingSearchSyncRequested> context)
    {
        if (!_options.Enabled)
        {
            LogSyncDisabled(_logger);
            return;
        }

        var message = context.Message;
        var listingId = message.ListingId;
        var eventType = message.EventType;

        LogSyncStarting(_logger, listingId, eventType);

        try
        {
            // Fetch-Latest-State: Luôn truy vấn trạng thái mới nhất từ SQL Server
            var listing = await _dbContext.Listings
                .Include(l => l.Images)
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.ListingId == listingId, context.CancellationToken);

            var now = DateTimeOffset.UtcNow;
            var isEligible = listing is not null
                && listing.Status == ListingStatus.Published
                && listing.ModerationDecision == ModerationDecision.Approved
                && listing.EndDate.HasValue
                && listing.EndDate.Value > now;

            var index = _meilisearchClient.Index(_options.ListingsIndexName);

            if (isEligible)
            {
                var document = ListingSearchDocument.FromEntity(listing!, utcNow: now);
                var task = await index.AddDocumentsAsync(new[] { document }, primaryKey: "listingId", cancellationToken: context.CancellationToken);
                LogUpserted(_logger, listingId, eventType, task.TaskUid);
            }
            else
            {
                var task = await index.DeleteOneDocumentAsync(
                    listingId.ToString(CultureInfo.InvariantCulture),
                    cancellationToken: context.CancellationToken);
                LogDeleted(_logger, listingId, eventType, task.TaskUid);
            }
        }
        catch (Exception ex)
        {
            LogSyncFailed(_logger, ex, listingId, eventType);
            throw; // Ném lại exception để MassTransit Retry hoạt động
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Meilisearch sync is disabled in configuration. Skipping sync.")]
    private static partial void LogSyncDisabled(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "SyncListingSearchConsumer: Processing sync for ListingId={ListingId} (Reason: {EventType}).")]
    private static partial void LogSyncStarting(ILogger logger, long listingId, string eventType);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "SyncListingSearchConsumer: Syncing ListingId={ListingId} (Reason: {EventType}) -> Upserted to Meilisearch (TaskUid: {TaskUid}).")]
    private static partial void LogUpserted(ILogger logger, long listingId, string eventType, int taskUid);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "SyncListingSearchConsumer: Syncing ListingId={ListingId} (Reason: {EventType}) -> Deleted from Meilisearch (TaskUid: {TaskUid}).")]
    private static partial void LogDeleted(ILogger logger, long listingId, string eventType, int taskUid);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "SyncListingSearchConsumer: Failed syncing ListingId={ListingId} (Reason: {EventType}).")]
    private static partial void LogSyncFailed(ILogger logger, Exception exception, long listingId, string eventType);
}

public sealed class SyncListingSearchConsumerDefinition : ConsumerDefinition<SyncListingSearchConsumer>
{
    public SyncListingSearchConsumerDefinition()
    {
        EndpointName = "listing-search-sync";
        ConcurrentMessageLimit = 1;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<SyncListingSearchConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(1)));
    }
}
