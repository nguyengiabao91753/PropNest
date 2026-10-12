using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropNest.Domain.Listings;

namespace PropNest.Infrastructure.Persistence.Configurations;

public sealed class ListingImageConfiguration : IEntityTypeConfiguration<ListingImage>
{
    public void Configure(EntityTypeBuilder<ListingImage> builder)
    {
        builder.ToTable("ListingImages");
        builder.HasKey(x => x.ImageId);

        builder.Property(x => x.Url)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.PublicId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Caption)
            .HasMaxLength(255);

        builder.Property(x => x.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.IsCover)
            .IsRequired();

        builder.HasIndex(x => new { x.ListingId, x.SortOrder });
    }
}
