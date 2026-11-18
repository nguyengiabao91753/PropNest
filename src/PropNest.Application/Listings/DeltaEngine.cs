namespace PropNest.Application.Listings;

public static class DeltaEngine
{
    public static List<ListingDelta> CreateDeltas(ListingDto before, ListingDto after)
    {
        var deltas = new List<ListingDelta>();

        AddDelta(deltas, nameof(ListingDto.Title), before.Title, after.Title);
        AddDelta(deltas, nameof(ListingDto.Description), before.Description, after.Description);
        AddDelta(deltas, nameof(ListingDto.PropertyType), before.PropertyType, after.PropertyType);
        AddDelta(deltas, nameof(ListingDto.ListingType), before.ListingType, after.ListingType);
        AddDelta(deltas, nameof(ListingDto.Price), before.Price, after.Price);
        AddDelta(deltas, nameof(ListingDto.Area), before.Area, after.Area);
        AddDelta(deltas, nameof(ListingDto.Floors), before.Floors, after.Floors);
        AddDelta(deltas, nameof(ListingDto.Bedrooms), before.Bedrooms, after.Bedrooms);
        AddDelta(deltas, nameof(ListingDto.Bathrooms), before.Bathrooms, after.Bathrooms);
        AddDelta(deltas, nameof(ListingDto.BalconyDirection), before.BalconyDirection, after.BalconyDirection);
        AddDelta(deltas, nameof(ListingDto.AdministrativeMode), before.AdministrativeMode, after.AdministrativeMode);
        AddDelta(deltas, nameof(ListingDto.City), before.City, after.City);
        AddDelta(deltas, nameof(ListingDto.District), before.District, after.District);
        AddDelta(deltas, nameof(ListingDto.Ward), before.Ward, after.Ward);
        AddDelta(deltas, nameof(ListingDto.Address), before.Address, after.Address);
        AddDelta(deltas, nameof(ListingDto.LegacyAddress), before.LegacyAddress, after.LegacyAddress);

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
