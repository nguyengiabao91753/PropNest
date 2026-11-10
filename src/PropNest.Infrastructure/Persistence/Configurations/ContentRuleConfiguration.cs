using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropNest.Domain.Listings;

namespace PropNest.Infrastructure.Persistence.Configurations;

public sealed class ContentRuleConfiguration : IEntityTypeConfiguration<ContentRule>
{
    private static readonly DateTimeOffset SeedTimestamp = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<ContentRule> builder)
    {
        builder.ToTable("ContentRules");
        builder.HasKey(x => x.RuleId);

        builder.Property(x => x.RuleType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Pattern)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Severity)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.RuleType, x.IsActive });

        builder.HasData(
            new ContentRule(
                ruleType: "BadWord",
                pattern: "lừa đảo|cờ bạc|cá độ|tín dụng đen",
                severity: "Reject",
                description: "Từ khóa vi phạm pháp luật hoặc lừa đảo nghiêm trọng",
                isActive: true,
                ruleId: 1,
                updatedAt: SeedTimestamp),
            new ContentRule(
                ruleType: "BadWord",
                pattern: "cam kết lợi nhuận 100%|x2 tài sản sau 1 tháng|sổ đỏ giả",
                severity: "Reject",
                description: "Từ khóa cam kết tài chính sai sự thật hoặc giấy tờ bất hợp pháp",
                isActive: true,
                ruleId: 2,
                updatedAt: SeedTimestamp),
            new ContentRule(
                ruleType: "PhonePattern",
                pattern: @"(?:\+84|0)(?:3|5|7|8|9)\d(?:[\s\.\-]?\d){7}",
                severity: "Reject",
                description: "Chặn chèn số điện thoại trực tiếp trong tiêu đề hoặc nội dung mô tả",
                isActive: true,
                ruleId: 3,
                updatedAt: SeedTimestamp),
            new ContentRule(
                ruleType: "Formatting",
                pattern: "UPPERCASE_RATIO_EXCEEDED:0.5",
                severity: "ManualReview",
                description: "Tỷ lệ chữ IN HOA vượt quá 50% gây phản cảm hoặc spam",
                isActive: true,
                ruleId: 4,
                updatedAt: SeedTimestamp),
            new ContentRule(
                ruleType: "Formatting",
                pattern: "MIN_DESCRIPTION_LENGTH:30",
                severity: "Warning",
                description: "Mô tả tin đăng quá ngắn (dưới 30 ký tự), thiếu thông tin chi tiết",
                isActive: true,
                ruleId: 5,
                updatedAt: SeedTimestamp));
    }
}
