using System.Text.Json;
using PropNest.Application.Abstractions;
using PropNest.Domain.Listings;
using PropNest.Domain.Workflows;

namespace PropNest.Application.Listings;

public sealed class ListingExpirationService(
    IListingRepository listingRepository,
    IUnitOfWork unitOfWork) : IListingExpirationService
{
    private const int BatchSize = 100;

    public async Task<int> ExpireListingsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var totalProcessed = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var candidates = await listingRepository.GetExpiredPublishedListingsAsync(now, BatchSize, cancellationToken);
            if (candidates.Count == 0)
            {
                break;
            }

            var batchUpdated = 0;
            foreach (var listing in candidates)
            {
                listing.Expire(now);

                if (listing.Status == ListingStatus.Expired)
                {
                    listingRepository.AddHistory(ListingHistory.CreateEvent(
                        listing.ListingId,
                        null,
                        "Expire"));

                    var payload = JsonSerializer.Serialize(new
                    {
                        listing.ListingId,
                        listing.OwnerUserId,
                        listing.PackageCode,
                        listing.EndDate
                    });
                    listingRepository.AddOutboxMessage(new OutboxMessage("ListingExpired", payload));
                    batchUpdated++;
                }
            }

            if (batchUpdated > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                totalProcessed += batchUpdated;
            }

            if (candidates.Count < BatchSize)
            {
                break;
            }
        }

        return totalProcessed;
    }
}
