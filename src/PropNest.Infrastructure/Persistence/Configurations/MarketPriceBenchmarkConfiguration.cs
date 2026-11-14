using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropNest.Domain.Listings;

namespace PropNest.Infrastructure.Persistence.Configurations;

public sealed class MarketPriceBenchmarkConfiguration : IEntityTypeConfiguration<MarketPriceBenchmark>
{
    private static readonly DateTimeOffset SeedEffectiveDate = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<MarketPriceBenchmark> builder)
    {
        builder.ToTable("MarketPriceBenchmarks");
        builder.HasKey(x => x.BenchmarkId);

        builder.Property(x => x.CityCode)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DistrictCode)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PropertyType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ListingType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.MinPricePerSqm)
            .HasPrecision(18, 2);

        builder.Property(x => x.AvgPricePerSqm)
            .HasPrecision(18, 2);

        builder.Property(x => x.MaxPricePerSqm)
            .HasPrecision(18, 2);

        builder.HasIndex(x => new { x.CityCode, x.DistrictCode, x.PropertyType, x.ListingType, x.EffectiveFrom })
            .IsUnique();

        builder.HasData(
            new MarketPriceBenchmark(
                cityCode: "TP. Hồ Chí Minh",
                districtCode: "Quận 1",
                propertyType: "Apartment",
                listingType: "Sale",
                minPricePerSqm: 80_000_000m,
                avgPricePerSqm: 150_000_000m,
                maxPricePerSqm: 350_000_000m,
                effectiveFrom: SeedEffectiveDate,
                benchmarkId: 1),
            new MarketPriceBenchmark(
                cityCode: "TP. Hồ Chí Minh",
                districtCode: "Quận 7",
                propertyType: "Apartment",
                listingType: "Sale",
                minPricePerSqm: 35_000_000m,
                avgPricePerSqm: 65_000_000m,
                maxPricePerSqm: 130_000_000m,
                effectiveFrom: SeedEffectiveDate,
                benchmarkId: 2),
            new MarketPriceBenchmark(
                cityCode: "TP. Hồ Chí Minh",
                districtCode: "Quận 7",
                propertyType: "House",
                listingType: "Sale",
                minPricePerSqm: 60_000_000m,
                avgPricePerSqm: 120_000_000m,
                maxPricePerSqm: 250_000_000m,
                effectiveFrom: SeedEffectiveDate,
                benchmarkId: 3),
            new MarketPriceBenchmark(
                cityCode: "TP. Hồ Chí Minh",
                districtCode: "Thành phố Thủ Đức",
                propertyType: "Apartment",
                listingType: "Sale",
                minPricePerSqm: 30_000_000m,
                avgPricePerSqm: 55_000_000m,
                maxPricePerSqm: 120_000_000m,
                effectiveFrom: SeedEffectiveDate,
                benchmarkId: 4),
            new MarketPriceBenchmark(
                cityCode: "Hà Nội",
                districtCode: "Quận Cầu Giấy",
                propertyType: "Apartment",
                listingType: "Sale",
                minPricePerSqm: 40_000_000m,
                avgPricePerSqm: 70_000_000m,
                maxPricePerSqm: 130_000_000m,
                effectiveFrom: SeedEffectiveDate,
                benchmarkId: 5),
            new MarketPriceBenchmark(
                cityCode: "TP. Hồ Chí Minh",
                districtCode: "Quận 7",
                propertyType: "Apartment",
                listingType: "Rent",
                minPricePerSqm: 120_000m,
                avgPricePerSqm: 220_000m,
                maxPricePerSqm: 450_000m,
                effectiveFrom: SeedEffectiveDate,
                benchmarkId: 6));
    }
}
