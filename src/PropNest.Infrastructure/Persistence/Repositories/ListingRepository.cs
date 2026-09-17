using Microsoft.EntityFrameworkCore;
using PropNest.Application.Listings;
using PropNest.Domain.Listings;

namespace PropNest.Infrastructure.Persistence.Repositories;

public sealed class ListingRepository(PropNestDbContext dbContext) : IListingRepository
{
    public Task<Listing?> GetByIdAsync(long listingId, CancellationToken cancellationToken = default) =>
        dbContext.Listings.SingleOrDefaultAsync(x => x.ListingId == listingId, cancellationToken);

    public Task AddAsync(Listing listing, CancellationToken cancellationToken = default) =>
        dbContext.Listings.AddAsync(listing, cancellationToken).AsTask();

    public void AddHistory(ListingHistory history) => dbContext.ListingHistories.Add(history);

    public void SetOriginalRowVersion(Listing listing, byte[] rowVersion)
    {
        if (rowVersion.Length == 0)
        {
            throw new ArgumentException("A RowVersion is required for updates.", nameof(rowVersion));
        }

        dbContext.Entry(listing).Property(x => x.RowVersion).OriginalValue = rowVersion;
    }
}
