using System.Net.Http;
using Meilisearch;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropNest.Application.Abstractions;

namespace PropNest.Infrastructure.Services.Search;

public sealed partial class MeilisearchIndexInitializer : IHostedService
{
    private readonly MeilisearchClient _client;
    private readonly MeilisearchOptions _options;
    private readonly ILogger<MeilisearchIndexInitializer> _logger;

    public MeilisearchIndexInitializer(
        MeilisearchClient client,
        IOptions<MeilisearchOptions> options,
        ILogger<MeilisearchIndexInitializer> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            LogMeilisearchDisabled(_logger);
            return;
        }

        try
        {
            LogVerifyingIndex(_logger, _options.ListingsIndexName, _options.Url);

            // 1. Kiểm tra / Tạo index với PrimaryKey là listingId
            Meilisearch.Index index;
            try
            {
                index = await _client.GetIndexAsync(_options.ListingsIndexName, cancellationToken);
                if (string.IsNullOrEmpty(index.PrimaryKey))
                {
                    var updateResult = await _client.UpdateIndexAsync(_options.ListingsIndexName, "listingId", cancellationToken);
                    await _client.WaitForTaskAsync(updateResult.TaskUid, cancellationToken: cancellationToken);
                }
            }
            catch (MeilisearchApiError ex) when (ex.Code == "index_not_found" || ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                LogCreatingIndex(_logger, _options.ListingsIndexName);
                var createResult = await _client.CreateIndexAsync(_options.ListingsIndexName, "listingId", cancellationToken);
                await _client.WaitForTaskAsync(createResult.TaskUid, cancellationToken: cancellationToken);
                index = await _client.GetIndexAsync(_options.ListingsIndexName, cancellationToken);
            }

            // 2. Cấu hình Index Settings
            var settings = new Settings
            {
                SearchableAttributes = new[]
                {
                    "title",
                    "address",
                    "ward",
                    "district",
                    "city",
                    "legacyAddress",
                    "searchNormalizedText",
                    "descriptionPlainText"
                },
                FilterableAttributes = new[]
                {
                    "listingId",
                    "status",
                    "moderationDecision",
                    "listingType",
                    "propertyType",
                    "price",
                    "area",
                    "floors",
                    "bedrooms",
                    "bathrooms",
                    "balconyDirection",
                    "administrativeMode",
                    "provinceCode",
                    "districtCode",
                    "wardCode",
                    "city",
                    "district",
                    "ward",
                    "locationTokens",
                    "packageCode",
                    "isVip",
                    "endDateUnix",
                    "packageEndDateUnix"
                },
                SortableAttributes = new[]
                {
                    "packageTierWeight",
                    "rankTimestampUnix",
                    "price",
                    "area",
                    "createdAtUnix"
                },
                RankingRules = new[]
                {
                    "words",
                    "typo",
                    "proximity",
                    "attribute",
                    "sort",
                    "exactness"
                },
                DisplayedAttributes = new[]
                {
                    "listingId",
                    "title",
                    "propertyType",
                    "listingType",
                    "price",
                    "area",
                    "floors",
                    "bedrooms",
                    "bathrooms",
                    "balconyDirection",
                    "administrativeMode",
                    "provinceCode",
                    "districtCode",
                    "wardCode",
                    "city",
                    "district",
                    "ward",
                    "address",
                    "legacyAddress",
                    "thumbnailUrl",
                    "status",
                    "moderationDecision",
                    "packageCode",
                    "packageEndDateUnix",
                    "endDateUnix",
                    "rankTimestampUnix",
                    "createdAtUnix"
                },
                Synonyms = new Dictionary<string, IEnumerable<string>>
                {
                    ["chung cu"] = new[] { "can ho", "apartment" },
                    ["can ho"] = new[] { "chung cu", "apartment" },
                    ["nha rieng"] = new[] { "nha pho", "house" },
                    ["nha pho"] = new[] { "nha rieng", "house" },
                    ["biet thu"] = new[] { "villa" },
                    ["villa"] = new[] { "biet thu" },
                    ["dat nen"] = new[] { "dat tho cu", "land" },
                    ["tphcm"] = new[] { "ho chi minh", "sai gon", "tp ho chi minh" },
                    ["hcm"] = new[] { "ho chi minh", "tp ho chi minh" },
                    ["hn"] = new[] { "ha noi" },
                    ["pn"] = new[] { "phong ngu" },
                    ["wc"] = new[] { "toilet", "nha ve sinh" }
                },
                TypoTolerance = new TypoTolerance
                {
                    Enabled = true,
                    MinWordSizeForTypos = new TypoTolerance.TypoSize
                    {
                        OneTypo = 4,
                        TwoTypos = 8
                    }
                }
            };

            var updateTask = await index.UpdateSettingsAsync(settings, cancellationToken);
            var finishedTask = await _client.WaitForTaskAsync(updateTask.TaskUid, cancellationToken: cancellationToken);

            LogSettingsSynchronized(
                _logger,
                _options.ListingsIndexName,
                finishedTask.Uid,
                finishedTask.Status.ToString());
        }
        catch (Exception ex)
        {
            LogSyncWarning(_logger, ex, _options.ListingsIndexName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Meilisearch is disabled in configuration.")]
    private static partial void LogMeilisearchDisabled(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Verifying Meilisearch index '{IndexName}' at {Url}...")]
    private static partial void LogVerifyingIndex(ILogger logger, string indexName, string url);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Index '{IndexName}' not found. Creating index with primaryKey 'listingId'...")]
    private static partial void LogCreatingIndex(ILogger logger, string indexName);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Meilisearch index '{IndexName}' verified and settings synchronized (PrimaryKey: listingId, TaskUid: {TaskUid}, Status: {Status}).")]
    private static partial void LogSettingsSynchronized(ILogger logger, string indexName, int taskUid, string status);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Unable to synchronize Meilisearch index '{IndexName}' during startup. Meilisearch may be offline or initializing.")]
    private static partial void LogSyncWarning(ILogger logger, Exception exception, string indexName);
}
