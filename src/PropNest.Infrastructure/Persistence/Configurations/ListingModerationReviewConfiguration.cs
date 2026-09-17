using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropNest.Domain.Listings;
using PropNest.Domain.Users;

namespace PropNest.Infrastructure.Persistence.Configurations;

public sealed class ListingModerationReviewConfiguration : IEntityTypeConfiguration<ListingModerationReview>
{
    public void Configure(EntityTypeBuilder<ListingModerationReview> builder)
    {
        builder.ToTable("ListingModerationReviews");
        builder.HasKey(x => x.ReviewId);
        builder.Property(x => x.Source).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Decision).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.ReasonsJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.ListingId, x.ReviewedAt });
        builder.HasOne<Listing>().WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
