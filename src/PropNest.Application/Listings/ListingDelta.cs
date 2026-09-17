namespace PropNest.Application.Listings;

public sealed record ListingDelta(
    string FieldName,
    object? OldValue,
    object? NewValue);
