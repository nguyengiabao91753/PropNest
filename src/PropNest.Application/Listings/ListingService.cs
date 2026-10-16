using System.Text.Json;
using PropNest.Application.Abstractions;
using PropNest.Application.Common;
using PropNest.Domain.Listings;
using PropNest.Domain.Workflows;

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

    public async Task<ListingCollectionDto> GetByOwnerAsync(long ownerUserId, CancellationToken cancellationToken = default)
    {
        var listings = await listingRepository.GetByOwnerUserIdAsync(ownerUserId, cancellationToken);
        var items = listings.Select(ToDto).ToList();
        return new ListingCollectionDto(items.Count, items);
    }

    public async Task<ListingCollectionDto> GetPendingModerationAsync(CancellationToken cancellationToken = default)
    {
        var listings = await listingRepository.GetPendingModerationAsync(cancellationToken);
        var items = listings.Select(ToDto).ToList();
        return new ListingCollectionDto(items.Count, items);
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
        var deltas = DeltaEngine.CreateDeltas(before, after);
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

    public async Task<PagedResult<ListingSummaryDto>> GetPublicListingsAsync(GetPublicListingsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query, nameof(query));

        var PageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var PageSize = query.PageSize < 1 ? 1 :
                       query.PageSize > 50 ? 50 : query.PageSize;

        var normalizedQuery = query with
        {
            PageNumber = PageNumber,
            PageSize = PageSize
        };

        return await listingRepository.GetPublicListingsAsync(normalizedQuery, cancellationToken);
    }

    public async Task HideAsync(long listingId, long ownerUserId, CancellationToken cancellationToken = default)
    {
        var listing = await GetOwnedListingAsync(listingId, ownerUserId, cancellationToken);
        if (listing is null)
        {
            throw new KeyNotFoundException($"Listing {listingId} was not found or does not belong to the user {ownerUserId}.");
        }

        if (listing.OwnerUserId != ownerUserId)
        {
            throw new UnauthorizedAccessException($"User {ownerUserId} does not have permission to hide listing {listingId}.");
        }

        if (listing.Status != ListingStatus.Published)
        {
            throw new ArgumentException("Only a published listing can be hidden.");
        }

        listing.Hide();
        listingRepository.AddHistory(ListingHistory.CreateEvent(listingId, ownerUserId, "Hide"));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ListingDto> ApproveAsync(long listingId, long moderatorUserId, CancellationToken cancellationToken = default)
    {
        var listing = await listingRepository.GetByIdAsync(listingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Listing {listingId} was not found.");
        var package = await listingRepository.GetPackageByCodeAsync(listing.PackageCode, cancellationToken)
            ?? throw new InvalidOperationException($"Package '{listing.PackageCode}' was not found.");

        var startDate = DateTimeOffset.UtcNow;
        var endDate = startDate.AddDays(package.DurationDays);
        listing.Approve(startDate, endDate);

        listingRepository.AddModerationReview(new ListingModerationReview(
            listing.ListingId,
            "Manual",
            ModerationDecision.Approved,
            null,
            moderatorUserId));
        listingRepository.AddHistory(ListingHistory.CreateEvent(
            listing.ListingId,
            moderatorUserId,
            "Approve"));

        var payload = JsonSerializer.Serialize(new
        {
            listing.ListingId,
            listing.OwnerUserId,
            listing.PackageCode,
            listing.StartDate,
            listing.EndDate
        });
        listingRepository.AddOutboxMessage(new OutboxMessage("ListingPublished", payload));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(listing);
    }

    public async Task<ListingDto> RejectAsync(long listingId, long moderatorUserId, string reasonsJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reasonsJson))
        {
            throw new ArgumentException("Rejection reasons are required.", nameof(reasonsJson));
        }

        var listing = await listingRepository.GetByIdAsync(listingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Listing {listingId} was not found.");

        listing.Reject();
        listingRepository.AddModerationReview(new ListingModerationReview(
            listing.ListingId,
            "Manual",
            ModerationDecision.Rejected,
            reasonsJson,
            moderatorUserId));
        listingRepository.AddHistory(ListingHistory.CreateEvent(
            listing.ListingId,
            moderatorUserId,
            "Reject",
            reasonsJson));

        var payload = JsonSerializer.Serialize(new
        {
            listing.ListingId,
            listing.OwnerUserId,
            ReasonsJson = reasonsJson
        });
        listingRepository.AddOutboxMessage(new OutboxMessage("ListingRejected", payload));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(listing);
    }

    public async Task<IReadOnlyList<ListingHistoryDto>> GetHistoriesAsync(long listingId, CancellationToken cancellationToken = default)
    {
        var histories = await listingRepository.GetHistoriesByListingIdAsync(listingId, cancellationToken);

        return histories;

    }
}
