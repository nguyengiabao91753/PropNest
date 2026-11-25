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

            // 2. Lấy toàn bộ documents hiện có từ Meilisearch theo từng trang (tránh giới hạn 10.000 doc)
            const int batchLimit = 1000;
            var meiliDocs = new Dictionary<long, ListingSearchDocument>();
            var offset = 0;
            while (!context.CancellationToken.IsCancellationRequested)
            {
                var docQuery = new DocumentsQuery
                {
                    Offset = offset,
                    Limit = batchLimit,
                    Fields = new List<string>
                    {
                        "listingId",
                        "updatedAtUnix",
                        "packageCode",
                        "isVip",
                        "endDateUnix",
                        "packageEndDateUnix",
                        "thumbnailUrl"
                    }
                };

                var batch = await index.GetDocumentsAsync<ListingSearchDocument>(docQuery, context.CancellationToken);
                var results = batch.Results.ToList();
                if (results.Count == 0)
                {
                    break;
                }

                foreach (var doc in results)
                {
                    meiliDocs[doc.ListingId] = doc;
                }

                offset += results.Count;
                if (results.Count < batchLimit || offset >= batch.Total)
                {
                    break;
                }
            }

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

            // 4. Tìm và bù tin thiếu hoặc sai lệch (Missing / Stale, bao gồm cả đổi ảnh bìa & hạ VIP)
            var docsToUpsert = new List<ListingSearchDocument>();
            foreach (var (id, sqlListing) in sqlMap)
            {
                var expectedDoc = ListingSearchDocument.FromEntity(sqlListing, utcNow: now);
                if (!meiliDocs.TryGetValue(id, out var meiliDoc))
                {
                    docsToUpsert.Add(expectedDoc);
                }
                else
                {
                    if (meiliDoc.UpdatedAtUnix != expectedDoc.UpdatedAtUnix
                        || !string.Equals(meiliDoc.PackageCode, expectedDoc.PackageCode, StringComparison.Ordinal)
                        || meiliDoc.IsVip != expectedDoc.IsVip
                        || meiliDoc.EndDateUnix != expectedDoc.EndDateUnix
                        || meiliDoc.PackageEndDateUnix != expectedDoc.PackageEndDateUnix
                        || !string.Equals(meiliDoc.ThumbnailUrl, expectedDoc.ThumbnailUrl, StringComparison.Ordinal))
                    {
                        docsToUpsert.Add(expectedDoc);
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
