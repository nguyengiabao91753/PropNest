using PropNest.Domain.Common;
using PropNest.Domain.Listings;

namespace PropNest.Domain.Tests;

public sealed class RichTextAndPackagePolicyTests
{
    [Fact]
    public void FormatRichDescriptionHtmlConvertsQuillBulletListAndRemovesUiSpan()
    {
        const string quillHtml =
            "<p><strong>Thông tin nổi bật:</strong></p>" +
            "<ol><li data-list=\"bullet\"><span class=\"ql-ui\" contenteditable=\"false\"></span><strong>Vị trí:</strong> Bắc Ninh</li>" +
            "<li data-list=\"bullet\"><span class=\"ql-ui\" contenteditable=\"false\"></span><strong>Diện tích:</strong> 122 m²</li></ol>";

        var formatted = RichTextHtmlHelper.FormatRichDescriptionHtml(quillHtml);

        Assert.DoesNotContain("ql-ui", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-list", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<ol>", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<ul><li><strong>Vị trí:</strong> Bắc Ninh</li><li><strong>Diện tích:</strong> 122 m²</li></ul>", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatRichDescriptionHtmlSanitizesScriptAndDangerousAttributes()
    {
        const string xssHtml =
            "<p onclick=\"alert(1)\">Mô tả hợp lệ</p>" +
            "<script>alert('xss')</script>" +
            "<a href=\"javascript:alert(2)\">bad</a>" +
            "<a href=\"https://propnest.vn\">safe</a>";

        var formatted = RichTextHtmlHelper.FormatRichDescriptionHtml(xssHtml);

        Assert.DoesNotContain("<script", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<p>Mô tả hợp lệ</p>", formatted, StringComparison.Ordinal);
        Assert.Contains("<a>bad</a>", formatted, StringComparison.Ordinal);
        Assert.Contains("<a href=\"https://propnest.vn\" target=\"_blank\" rel=\"noopener noreferrer\">safe</a>", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void ToPlainTextPreviewPreservesBulletPointsWithoutRawTags()
    {
        const string quillHtml =
            "<p><strong>Chính chủ bán nhà</strong></p>" +
            "<ol><li data-list=\"bullet\"><span class=\"ql-ui\" contenteditable=\"false\"></span><strong>Kết cấu:</strong> 2 tầng</li></ol>";

        var plain = RichTextHtmlHelper.ToPlainTextPreview(quillHtml);

        Assert.DoesNotContain("<", plain, StringComparison.Ordinal);
        Assert.Contains("Chính chủ bán nhà", plain, StringComparison.Ordinal);
        Assert.Contains("• Kết cấu: 2 tầng", plain, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ListingStatus.PendingPayment, ModerationDecision.Approved, true)]
    [InlineData(ListingStatus.Published, ModerationDecision.Approved, true)]
    [InlineData(ListingStatus.Expired, ModerationDecision.Approved, true)]
    [InlineData(ListingStatus.Draft, ModerationDecision.NotRequested, false)]
    [InlineData(ListingStatus.PendingModeration, ModerationDecision.Pending, false)]
    [InlineData(ListingStatus.Rejected, ModerationDecision.Rejected, false)]
    [InlineData(ListingStatus.Hidden, ModerationDecision.Approved, false)]
    [InlineData(ListingStatus.Deleted, ModerationDecision.Approved, false)]
    [InlineData(ListingStatus.PendingPayment, ModerationDecision.Pending, false)]
    public void CanPurchasePackageRequiresApprovedAndEligibleStatus(
        ListingStatus status,
        ModerationDecision decision,
        bool expected)
    {
        var actual = ListingPackagePolicy.CanPurchasePackage(status, decision);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ComputeNewAndExtendedEndDatesEndAtVietnamEndOfDay()
    {
        var nov18Vn = new DateTimeOffset(2026, 11, 18, 10, 15, 0, ListingPackagePolicy.VietnamUtcOffset);

        var vip7End = ListingPackagePolicy.ComputeNewEndDate(nov18Vn.ToUniversalTime(), 7);
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 23, 59, 59, ListingPackagePolicy.VietnamUtcOffset), vip7End);

        var stacked7End = ListingPackagePolicy.ExtendEndDate(vip7End, 7);
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 23, 59, 59, ListingPackagePolicy.VietnamUtcOffset), stacked7End);
    }

    [Theory]
    [InlineData("Standard", 30000, true, true)]
    [InlineData("VIP", 90000, true, true)]
    [InlineData("Boost", 30000, false, false)]
    [InlineData("Standard", 0, false, false)]
    [InlineData("Boost", 30000, true, false)]
    public void IsPurchasablePackageExcludesInactiveBoostAndZeroPrice(
        string kind,
        decimal price,
        bool isActive,
        bool expected)
    {
        Assert.Equal(expected, ListingPackagePolicy.IsPurchasablePackage(kind, price, isActive));
    }
}
