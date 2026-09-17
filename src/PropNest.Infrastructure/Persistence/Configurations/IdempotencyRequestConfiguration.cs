using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropNest.Domain.Workflows;

namespace PropNest.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRequestConfiguration : IEntityTypeConfiguration<IdempotencyRequest>
{
    public void Configure(EntityTypeBuilder<IdempotencyRequest> builder)
    {
        builder.ToTable("IdempotencyRequests");
        builder.HasKey(x => x.RequestId);
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RequestPath).HasMaxLength(300).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ResponseBody).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.UserId, x.Key }).IsUnique();
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasOne<WorkflowInstance>().WithMany().HasForeignKey(x => x.CorrelationId).OnDelete(DeleteBehavior.Restrict);
    }
}
