using PropNest.Application.Abstractions;
using PropNest.Domain.Listings;

namespace PropNest.Application.Listings;

public interface IListingModerationWatchdogService
{
    Task<int> EscalateStalePendingListingsAsync(TimeSpan? staleThreshold = null, CancellationToken cancellationToken = default);
}

public sealed class ListingModerationWatchdogService(
    IListingRepository listingRepository,
    IUnitOfWork unitOfWork) : IListingModerationWatchdogService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan DefaultStaleThreshold = TimeSpan.FromMinutes(10);

    public async Task<int> EscalateStalePendingListingsAsync(
        TimeSpan? staleThreshold = null,
        CancellationToken cancellationToken = default)
    {
        var threshold = staleThreshold ?? DefaultStaleThreshold;
        var cutoffUtc = DateTimeOffset.UtcNow - threshold;
        var escalatedTotal = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var staleListings = await listingRepository.GetStalePendingModerationListingsAsync(
                cutoffUtc,
                BatchSize,
                cancellationToken);

            if (staleListings.Count == 0)
            {
                break;
            }

            foreach (var listing in staleListings)
            {
                listing.MarkForManualReview();
                // Không ghi lịch sử nội bộ khi chuyển sang hàng chờ duyệt thủ công.
                escalatedTotal++;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (staleListings.Count < BatchSize)
            {
                break;
            }
        }

        return escalatedTotal;
    }
}
