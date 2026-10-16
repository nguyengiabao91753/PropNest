using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropNest.Domain.Workflows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Infrastructure.Persistence.Configurations
{
    public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
    {
        public void Configure(EntityTypeBuilder<InboxMessage> builder)
        {
            builder.ToTable("InboxMessages");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.MessageId)
                .IsRequired();
            builder.Property(x => x.ConsumerName)
                .HasMaxLength(200)
                .IsRequired();
            builder.Property(x => x.ProcessedOn)
                .IsRequired();
            // Khóa duy nhất (Unique Index): Đảm bảo 1 consumer không bao giờ xử lý 1 message 2 lần
            builder.HasIndex(x => new { x.ConsumerName, x.MessageId })
                .IsUnique();
        }
    }
}
