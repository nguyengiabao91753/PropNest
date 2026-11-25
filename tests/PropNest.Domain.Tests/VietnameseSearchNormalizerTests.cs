using PropNest.Application.Common;

namespace PropNest.Domain.Tests;

public class VietnameseSearchNormalizerTests
{
    [Theory]
    [InlineData("Đống Đa", "dong da")]
    [InlineData("đường", "duong")]
    [InlineData("Đà Nẵng", "da nang")]
    [InlineData("Thành phố Hồ Chí Minh", "thanh pho ho chi minh")]
    [InlineData("Tỉnh Bắc Ninh", "tinh bac ninh")]
    [InlineData("Căn hộ chung cư 2PN", "can ho chung cu 2pn")]
    [InlineData("  Khoảng   trắng   kép  ", "khoang trang kep")]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void NormalizeShouldNormalizeVietnameseAndDCorrectly(string? input, string expected)
    {
        var result = VietnameseSearchNormalizer.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildSearchNormalizedTextShouldCombineAndNormalizeFields()
    {
        var text = VietnameseSearchNormalizer.BuildSearchNormalizedText(
            "Bán căn hộ Evergreen Nếnh",
            "Tỉnh Bắc Ninh",
            "Thị xã Việt Yên",
            "Phường Việt Yên",
            "Chung cư Evergreen Nếnh",
            "Thành phố Bắc Ninh, Tỉnh Bắc Ninh"
        );

        Assert.Contains("ban can ho evergreen nenh", text);
        Assert.Contains("tinh bac ninh", text);
        Assert.Contains("thi xa viet yen", text);
        Assert.Contains("phuong viet yen", text);
        Assert.Contains("thanh pho bac ninh", text);
        Assert.DoesNotContain("Đ", text);
        Assert.DoesNotContain("đ", text);
    }

    [Fact]
    public void TokenizeLocationShouldReturnDistinctNonEmptyTokens()
    {
        var tokens = VietnameseSearchNormalizer.TokenizeLocation(
            "Tỉnh Bắc Ninh",
            "Thị xã Việt Yên",
            "Phường Việt Yên",
            "Tỉnh Bắc Ninh", // Trùng lặp
            null,
            "",
            "   "
        );

        Assert.Equal(3, tokens.Count);
        Assert.Contains("Tỉnh Bắc Ninh", tokens);
        Assert.Contains("Thị xã Việt Yên", tokens);
        Assert.Contains("Phường Việt Yên", tokens);
    }

    [Theory]
    [InlineData("Thành phố Hồ Chí Minh", "ho chi minh")]
    [InlineData("TP. Hồ Chí Minh", "ho chi minh")]
    [InlineData("TPHCM", "ho chi minh")]
    [InlineData("Sài Gòn", "ho chi minh")]
    [InlineData("Quận 7", "quan 7")]
    [InlineData("Quận Cầu Giấy", "cau giay")]
    [InlineData("Phường Việt Yên", "viet yen")]
    [InlineData("Thị xã Việt Yên", "viet yen")]
    [InlineData("Tỉnh Bắc Ninh", "bac ninh")]
    public void NormalizeLocationTokenShouldStripNonNumericPrefixesAndMapAliases(string input, string expected)
    {
        var result = VietnameseSearchNormalizer.NormalizeLocationToken(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildLocationFilterTokensShouldSplitLegacyAddressAndIncludeCanonicalTokens()
    {
        var tokens = VietnameseSearchNormalizer.BuildLocationFilterTokens(
            "Thành phố Hồ Chí Minh",
            "",
            "Phường Tân Phong",
            "Quận 7, TP. Hồ Chí Minh");

        Assert.Contains("ho chi minh", tokens);
        Assert.Contains("quan 7", tokens);
        Assert.Contains("tan phong", tokens);
        Assert.Contains("Quận 7", tokens);
    }
}
