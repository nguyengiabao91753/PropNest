using PropNest.Application.Common;
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

public sealed record ListingSummaryDto(
    long ListingId,
    string Title,
    decimal Price,
    decimal Area,
    string ListingType,
    string PropertyType,
    string City,
    string District,
    string? Ward,
    string PackageCode,
    string Address,
    ListingStatus Status,
    DateTimeOffset CreatedDate,
    ModerationDecision ModerationDecision);

public interface IListingRepository
{
    Task<Listing?> GetByIdAsync(long listingId, CancellationToken cancellationToken = default);

    Task AddAsync(Listing listing, CancellationToken cancellationToken = default);

    void AddHistory(ListingHistory history);

    void SetOriginalRowVersion(Listing listing, byte[] rowVersion);

    Task<PagedResult<ListingSummaryDto>> GetPublicListingsAsync(GetPublicListingsQuery query, CancellationToken cancellationToken = default);
}

public interface IListingService
{
    Task<ListingDto> CreateAsync(CreateListingCommand command, CancellationToken cancellationToken = default);

    Task<ListingDto?> GetByIdAsync(long listingId, CancellationToken cancellationToken = default);

    Task<ListingDto> UpdateAsync(UpdateListingCommand command, CancellationToken cancellationToken = default);

    Task SubmitAsync(long listingId, long ownerUserId, CancellationToken cancellationToken = default);

    Task<PagedResult<ListingSummaryDto>> GetPublicListingsAsync(GetPublicListingsQuery query, CancellationToken cancellationToken = default);
}

public sealed record GetPublicListingsQuery(
    string? District = null,
    string? City = null,
    string? Ward = null,
    string? PropertyType = null,
    string? ListingType = null,
    int PageNumber = 1,
    int PageSize = 20
    );
