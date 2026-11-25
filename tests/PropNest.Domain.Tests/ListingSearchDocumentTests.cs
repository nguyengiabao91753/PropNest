using PropNest.Application.Listings.Search;
using PropNest.Domain.Listings;

namespace PropNest.Domain.Tests;

public class ListingSearchDocumentTests
{
    [Fact]
    public void FromEntityShouldMapFieldsProperlyAndStripHtmlFromDescription()
    {
        var listing = new Listing(
            ownerUserId: 10,
            title: "Bán căn hộ Evergreen Nếnh 2 phòng ngủ",
            description: "<p>Chính chủ bán <strong>căn hộ</strong> đẹp tại Bắc Ninh.<ol><li data-list=\"bullet\">Gần trường học</li></ol></p>",
            propertyType: "Apartment",
            listingType: "Sale",
            price: 1_200_000_000m,
            area: 75.5m,
            city: "Tỉnh Bắc Ninh",
            district: "Thị xã Việt Yên",
            ward: "Phường Việt Yên",
            address: "Chung cư Evergreen Nếnh",
            floors: 2,
            bedrooms: 2,
            bathrooms: 2,
            balconyDirection: "Đông",
            administrativeMode: "PostMerger",
            provinceCode: 24,
            districtCode: 222,
            wardCode: 7888,
            legacyAddress: "Thành phố Bắc Ninh, Tỉnh Bắc Ninh");

        var doc = ListingSearchDocument.FromEntity(listing);

        Assert.Equal(listing.ListingId, doc.ListingId);
        Assert.Equal(listing.OwnerUserId, doc.OwnerUserId);
        Assert.Equal("Bán căn hộ Evergreen Nếnh 2 phòng ngủ", doc.Title);
        Assert.Equal("Apartment", doc.PropertyType);
        Assert.Equal("Sale", doc.ListingType);
        Assert.Equal(1_200_000_000m, doc.Price);
        Assert.Equal(75.5m, doc.Area);
        Assert.Equal(2, doc.Floors);
        Assert.Equal(2, doc.Bedrooms);
        Assert.Equal(2, doc.Bathrooms);
        Assert.Equal("Đông", doc.BalconyDirection);
        Assert.Equal(24, doc.ProvinceCode);
        Assert.Equal(222, doc.DistrictCode);
        Assert.Equal(7888, doc.WardCode);

        // Kiểm tra loại bỏ tag HTML khỏi mô tả
        Assert.DoesNotContain("<p>", doc.DescriptionPlainText);
        Assert.DoesNotContain("<strong>", doc.DescriptionPlainText);
        Assert.DoesNotContain("<ol>", doc.DescriptionPlainText);
        Assert.DoesNotContain("<li", doc.DescriptionPlainText);
        Assert.Contains("Chính chủ bán căn hộ đẹp tại Bắc Ninh", doc.DescriptionPlainText);

        // Kiểm tra chuỗi chuẩn hóa tìm kiếm tiếng Việt (gồm cả tiêu đề & mô tả)
        Assert.Equal("ban can ho evergreen nenh 2 phong ngu", doc.TitleNormalized);
        Assert.Contains("chinh chu ban can ho dep tai bac ninh", doc.DescriptionNormalizedText);
        Assert.Contains("ban can ho evergreen nenh 2 phong ngu", doc.SearchNormalizedText);
        Assert.Contains("tinh bac ninh", doc.SearchNormalizedText);
        Assert.Contains("thi xa viet yen", doc.SearchNormalizedText);
        Assert.Contains("bac ninh", doc.LocationTokens);
        Assert.Contains("viet yen", doc.LocationTokens);

        // Kiểm tra trọng số gói Standard
        Assert.False(doc.IsVip);
        Assert.Equal(100, doc.PackageTierWeight);
    }

    [Fact]
    public void FromEntityVipPackageShouldHaveWeight200()
    {
        var listing = new Listing(
            ownerUserId: 10,
            title: "Nhà phố VIP",
            description: "Mô tả",
            propertyType: "House",
            listingType: "Sale",
            price: 5_000_000_000m,
            area: 100m,
            city: "Hà Nội",
            district: "Cầu Giấy",
            ward: null,
            address: "Số 1 Cầu Giấy");

        // Sử dụng reflection hoặc method để giả lập gói VIP
        var prop = typeof(Listing).GetProperty(nameof(Listing.PackageCode));
        prop?.SetValue(listing, "VIP");

        var doc = ListingSearchDocument.FromEntity(listing);

        Assert.True(doc.IsVip);
        Assert.Equal(200, doc.PackageTierWeight);
    }

    [Fact]
    public void FromEntityExpiredVipPackageShouldDowngradeToStandardWeight100()
    {
        var listing = new Listing(
            ownerUserId: 10,
            title: "Nhà phố hết hạn VIP",
            description: "Mô tả chi tiết nhà phố chính chủ",
            propertyType: "House",
            listingType: "Sale",
            price: 5_000_000_000m,
            area: 100m,
            city: "Hà Nội",
            district: "Quận Cầu Giấy",
            ward: null,
            address: "Số 1 Cầu Giấy");

        var baseTime = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.FromHours(7));
        listing.SubmitForModeration();
        listing.Approve(baseTime);
        listing.ApplyPackagePurchase("Standard", 15, baseTime);
        listing.ApplyPackagePurchase("VIP", 3, baseTime);

        var afterVipExpired = baseTime.AddDays(5);
        var doc = ListingSearchDocument.FromEntity(listing, utcNow: afterVipExpired);

        Assert.False(doc.IsVip);
        Assert.Equal(100, doc.PackageTierWeight);
        Assert.Equal("Standard", doc.PackageCode);
        Assert.Null(doc.PackageEndDateUnix);
    }
}
