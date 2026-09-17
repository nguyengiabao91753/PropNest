using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropNest.Domain.Workflows;

namespace PropNest.Infrastructure.Persistence.Configurations;

public sealed class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
    {
        builder.ToTable("SagaListingStates");
        builder.HasKey(x => x.CorrelationId);
        builder.Property(x => x.PackageCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(100);
        builder.Property(x => x.ChargeAmount).HasPrecision(18, 2);
        builder.Property(x => x.ErrorMessage).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.Status, x.UpdatedAt });
    }
}
