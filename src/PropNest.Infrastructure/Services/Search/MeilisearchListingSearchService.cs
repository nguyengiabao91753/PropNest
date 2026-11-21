using System.Globalization;
using Meilisearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropNest.Application.Abstractions;
using PropNest.Application.Common;
using PropNest.Application.Listings;
using PropNest.Application.Listings.Search;
using PropNest.Domain.Listings;

namespace PropNest.Infrastructure.Services.Search;

public sealed partial class MeilisearchListingSearchService : IMeilisearchListingSearchService
{
    private readonly MeilisearchClient _client;
    private readonly MeilisearchOptions _options;
    private readonly IListingRepository _listingRepository;
    private readonly ILogger<MeilisearchListingSearchService> _logger;

    public MeilisearchListingSearchService(
        MeilisearchClient client,
        IOptions<MeilisearchOptions> options,
        IListingRepository listingRepository,
        ILogger<MeilisearchListingSearchService> logger)
    {
        _client = client;
        _options = options.Value;
        _listingRepository = listingRepository;
        _logger = logger;
    }

    public async Task<PagedResult<ListingSummaryDto>> SearchListingsAsync(GetPublicListingsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_options.Enabled)
        {
            LogFallbackDisabled(_logger);
            return await _listingRepository.GetPublicListingsAsync(query, cancellationToken);
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var timeoutMs = _options.RequestTimeoutMs > 0 ? _options.RequestTimeoutMs : 2000;
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

            var page = query.Page ?? (query.PageNumber > 0 ? query.PageNumber : 1);
            var pageSize = query.PageSize > 0 ? query.PageSize : 20;
            var offset = (page - 1) * pageSize;

            var filter = BuildFilter(query);
            var sort = BuildSort(query.SortBy);

            var index = _client.Index(_options.ListingsIndexName);

            // Normalize query truoc de dung trong AttributesToSearchOn
            var normalizedQ = string.IsNullOrWhiteSpace(query.Q)
                ? null
                : VietnameseSearchNormalizer.Normalize(query.Q);

            var searchQuery = new SearchQuery
            {
                Filter = filter,
                Sort = sort,
                Offset = offset,
                Limit = pageSize,
                // Chi tim trong searchNormalizedText khi co query de tranh false positives
                // do Meilisearch tu normalize Unicode cua cac field goc (title, city, v.v.)
                AttributesToSearchOn = string.IsNullOrWhiteSpace(normalizedQ)
                    ? null
                    : ["searchNormalizedText"]
            };

            var searchResult = await index.SearchAsync<ListingSearchDocument>(
                normalizedQ,
                searchQuery,
                timeoutCts.Token);

            var items = searchResult.Hits.Select(doc => new ListingSummaryDto(
                doc.ListingId,
                doc.Title,
                doc.Price,
                doc.Area,
                doc.ListingType,
                doc.PropertyType,
                doc.City,
                doc.District,
                doc.Ward,
                doc.PackageCode,
                doc.Address,
                ListingStatus.Published,
                DateTimeOffset.FromUnixTimeSeconds(doc.CreatedAtUnix),
                ModerationDecision.Approved,
                doc.Floors,
                doc.Bedrooms,
                doc.Bathrooms,
                doc.BalconyDirection,
                doc.AdministrativeMode,
                doc.LegacyAddress,
                doc.ThumbnailUrl,
                doc.ProvinceCode,
                doc.DistrictCode,
                doc.WardCode,
                doc.PackageEndDateUnix.HasValue ? DateTimeOffset.FromUnixTimeSeconds(doc.PackageEndDateUnix.Value) : null,
                doc.EndDateUnix.HasValue ? DateTimeOffset.FromUnixTimeSeconds(doc.EndDateUnix.Value) : null
            )).ToList();

            var totalCount = searchResult switch
            {
                SearchResult<ListingSearchDocument> sr => sr.EstimatedTotalHits,
                PaginatedSearchResult<ListingSearchDocument> psr => psr.TotalHits,
                _ => searchResult.Hits.Count
            };
            return new PagedResult<ListingSummaryDto>(items, totalCount, page, pageSize);
        }
        catch (Exception ex)
        {
            LogFallbackWarning(_logger, ex);
            return await _listingRepository.GetPublicListingsAsync(query, cancellationToken);
        }
    }

    private static string BuildFilter(GetPublicListingsQuery query)
    {
        var filters = new List<string>(8);
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 1. Hard filter bắt buộc cho mọi truy vấn công khai
        filters.Add("status = \"Published\"");
        filters.Add("moderationDecision = \"Approved\"");
        filters.Add($"endDateUnix > {nowUnix}");

        // 2. Listing Type (Sale / Rent)
        var rawType = query.Type ?? query.ListingType;
        if (!string.IsNullOrWhiteSpace(rawType))
        {
            var normalizedType = string.Equals(rawType, "rent", StringComparison.OrdinalIgnoreCase) ? "Rent" : "Sale";
            filters.Add($"listingType = \"{normalizedType}\"");
        }

        // 3. Property Type
        if (!string.IsNullOrWhiteSpace(query.PropertyType))
        {
            filters.Add($"propertyType = \"{Escape(query.PropertyType)}\"");
        }

        // 4. Giá (Price)
        if (query.MinPrice.HasValue)
        {
            filters.Add(string.Create(CultureInfo.InvariantCulture, $"price >= {query.MinPrice.Value}"));
        }
        if (query.MaxPrice.HasValue)
        {
            filters.Add(string.Create(CultureInfo.InvariantCulture, $"price <= {query.MaxPrice.Value}"));
        }

        // 5. Diện tích (Area)
        if (query.MinArea.HasValue)
        {
            filters.Add(string.Create(CultureInfo.InvariantCulture, $"area >= {query.MinArea.Value}"));
        }
        if (query.MaxArea.HasValue)
        {
            filters.Add(string.Create(CultureInfo.InvariantCulture, $"area <= {query.MaxArea.Value}"));
        }

        // 6. Số phòng ngủ & vệ sinh
        if (query.MinBedrooms.HasValue && query.MinBedrooms.Value > 0)
        {
            filters.Add(string.Create(CultureInfo.InvariantCulture, $"bedrooms >= {query.MinBedrooms.Value}"));
        }
        if (query.MinBathrooms.HasValue && query.MinBathrooms.Value > 0)
        {
            filters.Add(string.Create(CultureInfo.InvariantCulture, $"bathrooms >= {query.MinBathrooms.Value}"));
        }

        // 7. Hướng ban công
        if (!string.IsNullOrWhiteSpace(query.BalconyDirection))
        {
            filters.Add($"balconyDirection = \"{Escape(query.BalconyDirection)}\"");
        }

        // 8. Mã hành chính chuẩn hóa
        if (query.ProvinceCode.HasValue)
        {
            filters.Add(string.Create(CultureInfo.InvariantCulture, $"provinceCode = {query.ProvinceCode.Value}"));
        }
        if (query.DistrictCode.HasValue)
        {
            filters.Add(string.Create(CultureInfo.InvariantCulture, $"districtCode = {query.DistrictCode.Value}"));
        }
        if (query.WardCode.HasValue)
        {
            filters.Add(string.Create(CultureInfo.InvariantCulture, $"wardCode = {query.WardCode.Value}"));
        }

        // 9. Lọc địa chỉ theo chuỗi text (City, District, Ward)
        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var escapedCity = Escape(query.City);
            filters.Add($"(city = \"{escapedCity}\" OR locationTokens = \"{escapedCity}\")");
        }
        if (!string.IsNullOrWhiteSpace(query.District))
        {
            var escapedDistrict = Escape(query.District);
            filters.Add($"(district = \"{escapedDistrict}\" OR locationTokens = \"{escapedDistrict}\")");
        }
        if (!string.IsNullOrWhiteSpace(query.Ward))
        {
            var escapedWard = Escape(query.Ward);
            filters.Add($"(ward = \"{escapedWard}\" OR locationTokens = \"{escapedWard}\")");
        }

        return string.Join(" AND ", filters);
    }

    private static string[] BuildSort(string? sortBy)
    {
        return (sortBy?.ToLowerInvariant()) switch
        {
            "price_asc" => new[] { "price:asc" },
            "price_desc" => new[] { "price:desc" },
            "area_desc" => new[] { "area:desc" },
            "newest" => new[] { "createdAtUnix:desc" },
            _ => new[] { "packageTierWeight:desc", "rankTimestampUnix:desc" }
        };
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("\"", "\\\"", StringComparison.Ordinal)
                    .Trim();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Meilisearch search is disabled in configuration. Using SQL Server fallback.")]
    private static partial void LogFallbackDisabled(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Meilisearch search failed or timed out. Falling back to SQL Server query.")]
    private static partial void LogFallbackWarning(ILogger logger, Exception exception);
}
