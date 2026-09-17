using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropNest.Domain.Workflows;

namespace PropNest.Infrastructure.Persistence.Configurations;

public sealed class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<WorkflowStep> builder)
    {
        builder.ToTable("WorkflowSteps");
        builder.HasKey(x => x.StepId);
        builder.Property(x => x.StepName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Error).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.CorrelationId, x.StepName });
        builder.HasOne<WorkflowInstance>().WithMany().HasForeignKey(x => x.CorrelationId).OnDelete(DeleteBehavior.Cascade);
    }
}
