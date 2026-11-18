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

        // 1. Downgrade VIP -> Standard for listings whose VIP tier ended while base listing lifespan remains valid.
        await DowngradeExpiredVipPackagesAsync(now, cancellationToken);

        // 2. Expire published listings whose overall EndDate has elapsed.
        var totalExpired = 0;

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
                totalExpired += batchUpdated;
            }

            if (candidates.Count < BatchSize)
            {
                break;
            }
        }

        return totalExpired;
    }

    private async Task DowngradeExpiredVipPackagesAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var candidates = await listingRepository.GetListingsWithExpiredPackageAsync(now, BatchSize, cancellationToken);
            if (candidates.Count == 0)
            {
                break;
            }

            var batchUpdated = 0;
            foreach (var listing in candidates)
            {
                var previousPackage = listing.PackageCode;
                if (!listing.DowngradePackageIfExpired(now))
                {
                    continue;
                }

                listingRepository.AddHistory(ListingHistory.CreateEvent(
                    listing.ListingId,
                    null,
                    "DowngradePackage",
                    $"Hết hạn gói {previousPackage}, tự động chuyển về gói Standard (hiển thị đến {listing.EndDate?.ToOffset(ListingPackagePolicy.VietnamUtcOffset):dd/MM/yyyy HH:mm})"));
                batchUpdated++;
            }

            if (batchUpdated > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            if (candidates.Count < BatchSize)
            {
                break;
            }
        }
    }
}
