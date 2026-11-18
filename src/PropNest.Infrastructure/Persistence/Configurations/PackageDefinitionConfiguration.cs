using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropNest.Domain.Packages;

namespace PropNest.Infrastructure.Persistence.Configurations;

public sealed class PackageDefinitionConfiguration : IEntityTypeConfiguration<PackageDefinitions>
{
    public void Configure(EntityTypeBuilder<PackageDefinitions> builder)
    {
        builder.ToTable("PackageDefinitions");
        builder.HasKey(pd => pd.PackageId);
        builder.Property(x => x.PackageCode)
            .HasMaxLength(50)
            .IsRequired();
        builder.HasIndex(x => x.PackageCode)
            .IsUnique();
        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.Price)
            .HasPrecision(18, 2);
        builder.Property(x => x.Kind)
            .HasMaxLength(50)
            .IsRequired();

        // Legacy SKUs kept inactive for historical rows; active purchasable tiers are Standard & VIP (7/15/30d).
        builder.HasData(
            new PackageDefinitions(
                packageId: 1,
                packageCode: "Standard",
                name: "Gói Tiêu Chuẩn (Cũ)",
                price: 0m,
                durationDays: 15,
                kind: "Standard",
                isActive: false),
            new PackageDefinitions(
                packageId: 2,
                packageCode: "VIP",
                name: "Gói VIP (Cũ)",
                price: 150000m,
                durationDays: 30,
                kind: "VIP",
                isActive: false),
            new PackageDefinitions(
                packageId: 3,
                packageCode: "Boost",
                name: "Gói Đẩy Tin (Ẩn)",
                price: 30000m,
                durationDays: 7,
                kind: "Boost",
                isActive: false),
            new PackageDefinitions(
                packageId: 4,
                packageCode: "STANDARD_7",
                name: "Gói Tiêu Chuẩn - 7 ngày",
                price: 30000m,
                durationDays: 7,
                kind: "Standard",
                isActive: true),
            new PackageDefinitions(
                packageId: 5,
                packageCode: "STANDARD_15",
                name: "Gói Tiêu Chuẩn - 15 ngày",
                price: 55000m,
                durationDays: 15,
                kind: "Standard",
                isActive: true),
            new PackageDefinitions(
                packageId: 6,
                packageCode: "STANDARD_30",
                name: "Gói Tiêu Chuẩn - 30 ngày",
                price: 99000m,
                durationDays: 30,
                kind: "Standard",
                isActive: true),
            new PackageDefinitions(
                packageId: 7,
                packageCode: "VIP_7",
                name: "Gói VIP Nổi Bật - 7 ngày",
                price: 90000m,
                durationDays: 7,
                kind: "VIP",
                isActive: true),
            new PackageDefinitions(
                packageId: 8,
                packageCode: "VIP_15",
                name: "Gói VIP Nổi Bật - 15 ngày",
                price: 170000m,
                durationDays: 15,
                kind: "VIP",
                isActive: true),
            new PackageDefinitions(
                packageId: 9,
                packageCode: "VIP_30",
                name: "Gói VIP Nổi Bật - 30 ngày",
                price: 299000m,
                durationDays: 30,
                kind: "VIP",
                isActive: true));
    }
}
