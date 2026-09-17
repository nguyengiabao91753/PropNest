using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropNest.Domain.Listings;
using PropNest.Domain.Users;

namespace PropNest.Infrastructure.Persistence.Configurations;

public sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("Listings");
        builder.HasKey(x => x.ListingId);
        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Description).HasColumnType("nvarchar(max)");
        builder.Property(x => x.PropertyType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ListingType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.Area).HasPrecision(12, 2);
        builder.Property(x => x.City).HasMaxLength(100).IsRequired();
        builder.Property(x => x.District).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Ward).HasMaxLength(100);
        builder.Property(x => x.Address).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.ModerationDecision).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.PackageCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.OwnerUserId, x.Status });
        builder.HasIndex(x => new { x.Status, x.EndDate });
        builder.HasIndex(x => new { x.City, x.District, x.PropertyType });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Histories).WithOne().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
    }
}
