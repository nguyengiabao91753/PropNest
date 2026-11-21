namespace PropNest.Application.Listings.Search;

/// <summary>
/// Event yêu cầu đồng bộ trạng thái tin đăng sang Meilisearch.
/// Áp dụng mẫu Fetch-Latest-State: consumer chỉ dùng ListingId để truy vấn dữ liệu mới nhất từ SQL Server.
/// </summary>
public sealed record ListingSearchSyncRequested(
    long ListingId,
    string EventType,
    DateTimeOffset OccurredOn,
    Guid? CorrelationId = null);
