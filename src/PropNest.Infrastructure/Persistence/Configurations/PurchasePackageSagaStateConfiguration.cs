using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropNest.Infrastructure.Workflows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Infrastructure.Persistence.Configurations
{
    public class PurchasePackageSagaStateConfiguration : IEntityTypeConfiguration<PurchasePackageSagaState>
    {
        public void Configure(EntityTypeBuilder<PurchasePackageSagaState> builder)
        {
            builder.ToTable("PurchasePackageSagaStates");
            builder.HasKey(x => x.CorrelationId);
            builder.Property(x => x.CorrelationId).ValueGeneratedNever();
            builder.Property(x => x.CurrentState)
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(x => x.PackageCode)
                .HasMaxLength(50)
                .IsRequired();
            builder.Property(x => x.ChargeAmount)
                .HasPrecision(18, 2);
            builder.Property(x => x.ErrorMessage)
                .HasMaxLength(500);
            builder.Property(x => x.Version)
                .IsConcurrencyToken();
        }
    }
}
