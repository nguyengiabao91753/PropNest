using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropNest.Domain.Listings;

namespace PropNest.Infrastructure.Persistence.Configurations;

public sealed class AmeModerationResultConfiguration : IEntityTypeConfiguration<AmeModerationResult>
{
    public void Configure(EntityTypeBuilder<AmeModerationResult> builder)
    {
        builder.ToTable("AmeModerationResults");
        builder.HasKey(x => x.ResultId);

        builder.Property(x => x.Decision)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ScoreJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ReasonsJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.WarningsJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ModelInfo)
            .HasMaxLength(200);

        builder.HasIndex(x => x.CorrelationId)
            .IsUnique();

        builder.HasIndex(x => new { x.ListingId, x.CheckedAt });

        builder.HasOne<Listing>()
            .WithMany()
            .HasForeignKey(x => x.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
