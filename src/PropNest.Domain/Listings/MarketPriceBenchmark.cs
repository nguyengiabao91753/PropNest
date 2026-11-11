namespace PropNest.Domain.Listings;

public sealed class MarketPriceBenchmark
{
    private MarketPriceBenchmark()
    {
    }

    public MarketPriceBenchmark(
        string cityCode,
        string districtCode,
        string propertyType,
        string listingType,
        decimal minPricePerSqm,
        decimal avgPricePerSqm,
        decimal maxPricePerSqm,
        DateTimeOffset effectiveFrom,
        long benchmarkId = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cityCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(districtCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyType);
        ArgumentException.ThrowIfNullOrWhiteSpace(listingType);
        ArgumentOutOfRangeException.ThrowIfNegative(minPricePerSqm);

        if (avgPricePerSqm < minPricePerSqm)
        {
            throw new ArgumentOutOfRangeException(
                nameof(avgPricePerSqm),
                "Average price per sqm must be greater than or equal to minimum price per sqm.");
        }

        if (maxPricePerSqm < avgPricePerSqm)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPricePerSqm),
                "Maximum price per sqm must be greater than or equal to average price per sqm.");
        }

        BenchmarkId = benchmarkId;
        CityCode = cityCode.Trim();
        DistrictCode = districtCode.Trim();
        PropertyType = propertyType.Trim();
        ListingType = listingType.Trim();
        MinPricePerSqm = minPricePerSqm;
        AvgPricePerSqm = avgPricePerSqm;
        MaxPricePerSqm = maxPricePerSqm;
        EffectiveFrom = effectiveFrom;
    }

    public long BenchmarkId { get; private set; }

    public string CityCode { get; private set; } = string.Empty;

    public string DistrictCode { get; private set; } = string.Empty;

    public string PropertyType { get; private set; } = string.Empty;

    public string ListingType { get; private set; } = string.Empty;

    public decimal MinPricePerSqm { get; private set; }

    public decimal AvgPricePerSqm { get; private set; }

    public decimal MaxPricePerSqm { get; private set; }

    public DateTimeOffset EffectiveFrom { get; private set; }
}
