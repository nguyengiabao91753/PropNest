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

            var rawPage = query.Page ?? query.PageNumber;
            var page = rawPage > 0 ? rawPage : 1;
            var pageSize = Math.Clamp(query.PageSize > 0 ? query.PageSize : 20, 1, 50);
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var filter = BuildFilter(query, nowUnix);
            var sort = BuildSort(query.SortBy);

            var index = _client.Index(_options.ListingsIndexName);

            // Chuẩn hóa tiếng Việt (đ/Đ -> d/D, bỏ dấu) nhưng tìm trên toàn bộ SearchableAttributes
            // (title > titleNormalized > address > ... > searchNormalizedText > descriptionPlainText > descriptionNormalizedText)
            // để giữ nguyên thứ tự ưu tiên thuộc tính và không bỏ sót nội dung mô tả.
            var normalizedQ = string.IsNullOrWhiteSpace(query.Q)
                ? null
                : VietnameseSearchNormalizer.Normalize(query.Q);

            var searchQuery = new SearchQuery
            {
                Filter = filter,
                Sort = sort,
                Page = page,
                HitsPerPage = pageSize
            };

            var searchResult = await index.SearchAsync<ListingSearchDocument>(
                normalizedQ,
                searchQuery,
                timeoutCts.Token);

            var items = searchResult.Hits.Select(doc =>
            {
                // Lớp 4 Zero-Drift: phòng thủ giao ngày lúc 00:00:00-00:00:05 nếu gói VIP vừa hết hạn trước khi Quartz kịp hạ
                var isVipExpired = string.Equals(doc.PackageCode, ListingPackagePolicy.VipPackageCode, StringComparison.OrdinalIgnoreCase)
                    && doc.PackageEndDateUnix.HasValue
                    && doc.PackageEndDateUnix.Value <= nowUnix;

                var effectivePackageCode = isVipExpired
                    ? ListingPackagePolicy.StandardPackageCode
                    : doc.PackageCode;

                DateTimeOffset? effectivePackageEndDate = isVipExpired
                    ? null
                    : (doc.PackageEndDateUnix.HasValue ? DateTimeOffset.FromUnixTimeSeconds(doc.PackageEndDateUnix.Value) : null);

                return new ListingSummaryDto(
                    doc.ListingId,
                    doc.Title,
                    doc.Price,
                    doc.Area,
                    doc.ListingType,
                    doc.PropertyType,
                    doc.City,
                    doc.District,
                    doc.Ward,
                    effectivePackageCode,
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
                    effectivePackageEndDate,
                    doc.EndDateUnix.HasValue ? DateTimeOffset.FromUnixTimeSeconds(doc.EndDateUnix.Value) : null);
            }).ToList();

            var totalCount = searchResult switch
            {
                PaginatedSearchResult<ListingSearchDocument> psr => psr.TotalHits,
                SearchResult<ListingSearchDocument> sr => sr.EstimatedTotalHits,
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

    public static string BuildFilter(GetPublicListingsQuery query, long nowUnix)
    {
        ArgumentNullException.ThrowIfNull(query);
        var filters = new List<string>(8);

        // 1. Hard filter bắt buộc cho mọi truy vấn công khai
        filters.Add("status = \"Published\"");
        filters.Add("moderationDecision = \"Approved\"");
        filters.Add($"endDateUnix > {nowUnix}");

        // 2. Listing Type (Sale / Rent; bỏ qua khi 'all')
        var rawType = (query.ListingType ?? query.Type)?.Trim();
        if (!string.IsNullOrWhiteSpace(rawType) && !string.Equals(rawType, "all", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedType = rawType.ToLowerInvariant() switch
            {
                "rent" => "Rent",
                "sale" => "Sale",
                _ => Escape(rawType)
            };
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

        // 9. Lọc địa chỉ theo chuỗi text (khớp cả gốc, không dấu, rút gọn tiền tố và LegacyAddress)
        AddLocationFilter(filters, "city", query.City);
        AddLocationFilter(filters, "district", query.District);
        AddLocationFilter(filters, "ward", query.Ward);

        return string.Join(" AND ", filters);
    }

    private static void AddLocationFilter(List<string> filters, string fieldName, string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return;
        }

        var escapedRaw = Escape(rawValue);
        var normalized = VietnameseSearchNormalizer.NormalizeLocationToken(rawValue);
        if (!string.IsNullOrWhiteSpace(normalized) && !string.Equals(normalized, escapedRaw, StringComparison.OrdinalIgnoreCase))
        {
            var escapedNorm = Escape(normalized);
            filters.Add($"({fieldName} = \"{escapedRaw}\" OR locationTokens = \"{escapedRaw}\" OR locationTokens = \"{escapedNorm}\")");
        }
        else
        {
            filters.Add($"({fieldName} = \"{escapedRaw}\" OR locationTokens = \"{escapedRaw}\")");
        }
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
