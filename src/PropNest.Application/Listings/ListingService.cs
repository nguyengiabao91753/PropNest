using System.Text.Json;
using PropNest.Application.Abstractions;
using PropNest.Domain.Listings;

namespace PropNest.Application.Listings;

public sealed class ListingService(IListingRepository listingRepository, IUnitOfWork unitOfWork) : IListingService
{
    public async Task<ListingDto> CreateAsync(CreateListingCommand command, CancellationToken cancellationToken = default)
    {
        var listing = new Listing(
            command.OwnerUserId,
            command.Title,
            command.Description,
            command.PropertyType,
            command.ListingType,
            command.Price,
            command.Area,
            command.City,
            command.District,
            command.Ward,
            command.Address);

        await listingRepository.AddAsync(listing, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        listingRepository.AddHistory(ListingHistory.CreateSnapshot(
            listing.ListingId,
            command.OwnerUserId,
            "Create",
            JsonSerializer.Serialize(ToDto(listing))));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(listing);
    }

    public async Task<ListingDto?> GetByIdAsync(long listingId, CancellationToken cancellationToken = default)
    {
        var listing = await listingRepository.GetByIdAsync(listingId, cancellationToken);
        return listing is null ? null : ToDto(listing);
    }

    public async Task<ListingDto> UpdateAsync(UpdateListingCommand command, CancellationToken cancellationToken = default)
    {
        var listing = await GetOwnedListingAsync(command.ListingId, command.OwnerUserId, cancellationToken);
        var before = ToDto(listing);
        listingRepository.SetOriginalRowVersion(listing, command.RowVersion);

        listing.UpdateContent(
            command.Title,
            command.Description,
            command.PropertyType,
            command.ListingType,
            command.Price,
            command.Area,
            command.City,
            command.District,
            command.Ward,
            command.Address);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        var after = ToDto(listing);
        var deltas = CreateDeltas(before, after);
        listingRepository.AddHistory(ListingHistory.CreateDelta(
            listing.ListingId,
            command.OwnerUserId,
            "UpdateContent",
            JsonSerializer.Serialize(deltas)));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return after;
    }

    public async Task SubmitAsync(long listingId, long ownerUserId, CancellationToken cancellationToken = default)
    {
        var listing = await GetOwnedListingAsync(listingId, ownerUserId, cancellationToken);
        listing.SubmitForModeration();
        listingRepository.AddHistory(ListingHistory.CreateEvent(listingId, ownerUserId, "Submit"));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Listing> GetOwnedListingAsync(long listingId, long ownerUserId, CancellationToken cancellationToken)
    {
        var listing = await listingRepository.GetByIdAsync(listingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Listing {listingId} was not found.");

        if (listing.OwnerUserId != ownerUserId)
        {
            throw new UnauthorizedAccessException("The current user does not own this listing.");
        }

        return listing;
    }

    private static ListingDto ToDto(Listing listing) => new(
        listing.ListingId,
        listing.OwnerUserId,
        listing.Title,
        listing.Description,
        listing.PropertyType,
        listing.ListingType,
        listing.Price,
        listing.Area,
        listing.City,
        listing.District,
        listing.Ward,
        listing.Address,
        listing.Status,
        listing.ModerationDecision,
        listing.PackageCode,
        listing.StartDate,
        listing.EndDate,
        listing.RowVersion);

    private static List<ListingDelta> CreateDeltas(ListingDto before, ListingDto after)
    {
        var deltas = new List<ListingDelta>();

        AddDelta(deltas, nameof(ListingDto.Title), before.Title, after.Title);
        AddDelta(deltas, nameof(ListingDto.Description), before.Description, after.Description);
        AddDelta(deltas, nameof(ListingDto.PropertyType), before.PropertyType, after.PropertyType);
        AddDelta(deltas, nameof(ListingDto.ListingType), before.ListingType, after.ListingType);
        AddDelta(deltas, nameof(ListingDto.Price), before.Price, after.Price);
        AddDelta(deltas, nameof(ListingDto.Area), before.Area, after.Area);
        AddDelta(deltas, nameof(ListingDto.City), before.City, after.City);
        AddDelta(deltas, nameof(ListingDto.District), before.District, after.District);
        AddDelta(deltas, nameof(ListingDto.Ward), before.Ward, after.Ward);
        AddDelta(deltas, nameof(ListingDto.Address), before.Address, after.Address);

        return deltas;
    }

    private static void AddDelta<T>(List<ListingDelta> deltas, string fieldName, T oldValue, T newValue)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            deltas.Add(new ListingDelta(fieldName, oldValue, newValue));
        }
    }
}
