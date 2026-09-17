using PropNest.Domain.Listings;

namespace PropNest.Application.Listings;

public sealed record CreateListingCommand(
    long OwnerUserId,
    string Title,
    string? Description,
    string PropertyType,
    string ListingType,
    decimal Price,
    decimal Area,
    string City,
    string District,
    string? Ward,
    string Address);

public sealed record UpdateListingCommand(
    long ListingId,
    long OwnerUserId,
    string Title,
    string? Description,
    string PropertyType,
    string ListingType,
    decimal Price,
    decimal Area,
    string City,
    string District,
    string? Ward,
    string Address,
    byte[] RowVersion);

public sealed record ListingDto(
    long ListingId,
    long OwnerUserId,
    string Title,
    string? Description,
    string PropertyType,
    string ListingType,
    decimal Price,
    decimal Area,
    string City,
    string District,
    string? Ward,
    string Address,
    ListingStatus Status,
    ModerationDecision ModerationDecision,
    string PackageCode,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    byte[] RowVersion);

public interface IListingRepository
{
    Task<Listing?> GetByIdAsync(long listingId, CancellationToken cancellationToken = default);

    Task AddAsync(Listing listing, CancellationToken cancellationToken = default);

    void AddHistory(ListingHistory history);

    void SetOriginalRowVersion(Listing listing, byte[] rowVersion);
}

public interface IListingService
{
    Task<ListingDto> CreateAsync(CreateListingCommand command, CancellationToken cancellationToken = default);

    Task<ListingDto?> GetByIdAsync(long listingId, CancellationToken cancellationToken = default);

    Task<ListingDto> UpdateAsync(UpdateListingCommand command, CancellationToken cancellationToken = default);

    Task SubmitAsync(long listingId, long ownerUserId, CancellationToken cancellationToken = default);
}
