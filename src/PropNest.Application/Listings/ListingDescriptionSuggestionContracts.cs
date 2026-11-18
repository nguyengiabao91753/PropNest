namespace PropNest.Application.Listings;

public sealed record SuggestListingDescriptionCommand(
    string? Title,
    string PropertyType,
    string ListingType,
    decimal? Price,
    decimal? Area,
    string? City,
    string? District,
    string? Ward,
    string? Address,
    string? LegacyAddress,
    string? AdministrativeMode,
    int? Floors,
    int? Bedrooms,
    int? Bathrooms,
    string? BalconyDirection);

public sealed record ListingDescriptionSuggestionResult(
    string SuggestedTitle,
    string SuggestedDescription,
    string Source,
    string? ModelName);

public interface IListingDescriptionGenerator
{
    Task<ListingDescriptionSuggestionResult> GenerateDescriptionAsync(
        SuggestListingDescriptionCommand command,
        CancellationToken cancellationToken = default);
}
