using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropNest.Domain.Packages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Infrastructure.Persistence.Configurations
{
    public sealed class PackageDefinitionConfiguration : IEntityTypeConfiguration<PackageDefinitions>
    {
        public void Configure(EntityTypeBuilder<PackageDefinitions> builder)
        {
            builder.ToTable("PackageDefinitions");
            builder.HasKey(pd => pd.PackageId);
            builder.Property(x => x.PackageCode)
            .HasMaxLength(50)
            .IsRequired();
            builder.HasIndex(x => x.PackageCode)
                .IsUnique();
            builder.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();
            builder.Property(x => x.Price)
                .HasPrecision(18, 2);
            builder.Property(x => x.Kind)
                .HasMaxLength(50)
                .IsRequired();

            // Seed data
            builder.HasData(
           new PackageDefinitions(
               packageId: 1,
               packageCode: "Standard",
               name: "Gói Tiêu Chuẩn",
               price: 50000m,
               durationDays: 30,
               kind: "Standard",
               isActive: true),
           new PackageDefinitions(
               packageId: 2,
               packageCode: "VIP",
               name: "Gói VIP",
               price: 150000m,
               durationDays: 30,
               kind: "VIP",
               isActive: true),
           new PackageDefinitions(
               packageId: 3,
               packageCode: "Boost",
               name: "Gói Đẩy Tin",
               price: 30000m,
               durationDays: 7,
               kind: "Boost",
               isActive: true)
       );
        }
    }
}
