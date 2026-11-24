using System.Globalization;
using Meilisearch;
using Meilisearch.QueryParameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings.Search;
using PropNest.Domain.Listings;
using PropNest.Infrastructure.Persistence;
using Quartz;

namespace PropNest.Workflow.Worker.Jobs;

[DisallowConcurrentExecution]
public sealed partial class SearchReconciliationJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SearchReconciliationJob> _logger;

    public SearchReconciliationJob(
        IServiceScopeFactory scopeFactory,
        ILogger<SearchReconciliationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _scopeFactory.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<MeilisearchOptions>>().Value;

        if (!options.Enabled)
        {
            LogJobDisabled(_logger);
            return;
        }

        LogJobStarted(_logger);

        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PropNestDbContext>();
            var client = scope.ServiceProvider.GetRequiredService<MeilisearchClient>();
            var index = client.Index(options.ListingsIndexName);

            // 1. Lấy toàn bộ tin đăng đang Published & hợp lệ từ SQL Server
            var now = DateTimeOffset.UtcNow;
            var sqlPublishedListings = await dbContext.Listings
                .Include(l => l.Images)
                .AsNoTracking()
                .Where(l => l.Status == ListingStatus.Published
                         && l.ModerationDecision == ModerationDecision.Approved
                         && l.EndDate.HasValue
                         && l.EndDate.Value > now)
                .ToListAsync(context.CancellationToken);

            var sqlMap = sqlPublishedListings.ToDictionary(l => l.ListingId);

            // 2. Lấy toàn bộ documents hiện có từ Meilisearch
            var docQuery = new DocumentsQuery
            {
                Limit = 10000,
                Fields = new List<string> { "listingId", "updatedAtUnix", "packageCode", "endDateUnix", "packageEndDateUnix" }
            };

            var meiliResults = await index.GetDocumentsAsync<ListingSearchDocument>(docQuery, context.CancellationToken);
            var meiliDocs = meiliResults.Results.ToDictionary(d => d.ListingId);

            // 3. Tìm và xóa tin rác (Orphan): Tồn tại trên Meilisearch nhưng SQL không còn Published
            var orphanIds = meiliDocs.Keys
                .Where(id => !sqlMap.ContainsKey(id))
                .Select(id => id.ToString(CultureInfo.InvariantCulture))
                .ToList();

            if (orphanIds.Count > 0)
            {
                await index.DeleteDocumentsAsync(orphanIds, context.CancellationToken);
                LogOrphansPruned(_logger, orphanIds.Count);
            }

            // 4. Tìm và bù tin thiếu hoặc sai lệch (Missing / Stale)
            var docsToUpsert = new List<ListingSearchDocument>();
            foreach (var (id, sqlListing) in sqlMap)
            {
                if (!meiliDocs.TryGetValue(id, out var meiliDoc))
                {
                    docsToUpsert.Add(ListingSearchDocument.FromEntity(sqlListing));
                }
                else
                {
                    var sqlUpdatedUnix = sqlListing.UpdatedAt.ToUnixTimeSeconds();
                    var sqlEndUnix = sqlListing.EndDate?.ToUnixTimeSeconds();
                    var sqlPkgEndUnix = sqlListing.PackageEndDate?.ToUnixTimeSeconds();

                    if (meiliDoc.UpdatedAtUnix != sqlUpdatedUnix
                        || meiliDoc.PackageCode != sqlListing.PackageCode
                        || meiliDoc.EndDateUnix != sqlEndUnix
                        || meiliDoc.PackageEndDateUnix != sqlPkgEndUnix)
                    {
                        docsToUpsert.Add(ListingSearchDocument.FromEntity(sqlListing));
                    }
                }
            }

            if (docsToUpsert.Count > 0)
            {
                await index.AddDocumentsAsync(docsToUpsert, primaryKey: "listingId", cancellationToken: context.CancellationToken);
                LogRepaired(_logger, docsToUpsert.Count);
            }

            LogJobFinished(_logger, sqlMap.Count, meiliDocs.Count, orphanIds.Count, docsToUpsert.Count);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // Bỏ qua khi ứng dụng đang tắt
        }
        catch (Exception ex)
        {
            LogJobFailed(_logger, ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "SearchReconciliationJob is disabled in configuration.")]
    private static partial void LogJobDisabled(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "SearchReconciliationJob started: Comparing SQL Server vs Meilisearch...")]
    private static partial void LogJobStarted(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "SearchReconciliationJob pruned {Count} orphan document(s) from Meilisearch.")]
    private static partial void LogOrphansPruned(ILogger logger, int count);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "SearchReconciliationJob repaired/upserted {Count} missing or stale document(s) to Meilisearch.")]
    private static partial void LogRepaired(ILogger logger, int count);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "SearchReconciliationJob finished. SQLPublished: {SqlCount}, MeiliDocs: {MeiliCount}, Pruned: {PrunedCount}, Repaired: {RepairedCount}.")]
    private static partial void LogJobFinished(ILogger logger, int sqlCount, int meiliCount, int prunedCount, int repairedCount);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "SearchReconciliationJob failed during execution.")]
    private static partial void LogJobFailed(ILogger logger, Exception exception);
}
