using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropNest.Domain.Workflows;

namespace PropNest.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(x => x.OutboxId);
        builder.Property(x => x.Type).HasMaxLength(255).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Error).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => x.ProcessedOn).HasFilter("[ProcessedOn] IS NULL");
    }
}
