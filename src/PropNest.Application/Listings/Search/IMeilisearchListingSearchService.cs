using PropNest.Application.Common;

namespace PropNest.Application.Listings.Search;

/// <summary>
/// Service tìm kiếm tin đăng bất động sản thông qua Meilisearch với cơ chế SQL Fallback.
/// </summary>
public interface IMeilisearchListingSearchService
{
    Task<PagedResult<ListingSummaryDto>> SearchListingsAsync(GetPublicListingsQuery query, CancellationToken cancellationToken = default);
}
