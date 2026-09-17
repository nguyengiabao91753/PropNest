using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropNest.Domain.Listings;
using PropNest.Domain.Users;

namespace PropNest.Infrastructure.Persistence.Configurations;

public sealed class ListingHistoryConfiguration : IEntityTypeConfiguration<ListingHistory>
{
    public void Configure(EntityTypeBuilder<ListingHistory> builder)
    {
        builder.ToTable("ListingHistories");
        builder.HasKey(x => x.HistoryId);
        builder.Property(x => x.ActionType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.DeltasJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => new { x.ListingId, x.ActionDate });
        builder.HasOne<Listing>().WithMany(x => x.Histories).HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
