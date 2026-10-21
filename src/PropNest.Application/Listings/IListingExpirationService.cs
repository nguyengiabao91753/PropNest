namespace PropNest.Application.Listings;

public interface IListingExpirationService
{
    Task<int> ExpireListingsAsync(CancellationToken cancellationToken = default);
}
