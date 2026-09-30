using Microsoft.EntityFrameworkCore;
using PropNest.Application.Common;
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

    public async Task<PagedResult<ListingSummaryDto>> GetPublicListingsAsync(GetPublicListingsQuery query, CancellationToken cancellationToken = default)
    {
        var queryable = dbContext.Listings.AsNoTracking()
                           .Where(x => x.Status == ListingStatus.Published)
                           .Where(x => query.District == null || x.District == query.District)
                           .Where(x => query.City == null || x.City == query.City)
                           .Where(x => query.Ward == null || x.Ward == query.Ward)
                           .Where(x => query.PropertyType == null || x.PropertyType == query.PropertyType)
                           .Where(x => query.ListingType == null || x.ListingType == query.ListingType);
                          
        var totalCount = await queryable.CountAsync(cancellationToken);

        var items = await queryable
            .OrderByDescending(x => x.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new ListingSummaryDto(
                x.ListingId,
                x.Title,
                x.Price,
                x.Area,
                x.ListingType,
                x.PropertyType,
                x.City,
                x.District,
                x.Ward,
                x.PackageCode,
                x.Address,
                x.Status,
                x.CreatedAt,
                x.ModerationDecision
                ))
            .ToListAsync(cancellationToken);

        return new PagedResult<ListingSummaryDto>(items, totalCount, query.PageNumber, query.PageSize);
    }
}
