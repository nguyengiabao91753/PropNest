using PropNest.Domain.Listings;

namespace PropNest.Web.Seller.Models.ViewModels;

public sealed class SellerPropertyItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PropertyType { get; set; } = "Apartment";
    public string ListingType { get; set; } = "sale"; // 'sale' | 'rent'
    public decimal Price { get; set; }
    public decimal Area { get; set; }
    public decimal LandSize { get; set; }
    public int Beds { get; set; }
    public int Baths { get; set; }
    public int Carports { get; set; } = 1;
    public string Condition { get; set; } = "Nhà mới đẹp";
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string? Ward { get; set; }
    public string Address { get; set; } = string.Empty;
    public ListingStatus Status { get; set; } = ListingStatus.Draft;
    public string PackageCode { get; set; } = "Standard"; // 'Standard' | 'VIP' | 'Boost'
    public string SellerId { get; set; } = "seller-1";
    public string SellerName { get; set; } = "Nguyễn Văn Minh";
    public string? SellerAvatar { get; set; }
    public List<string> Images { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? RejectReason { get; set; }
    public string RowVersion { get; set; } = "AAAAAA==";
    public int Views { get; set; } = 142;
    public int Inquiries { get; set; } = 12;
    public string? Banner { get; set; } = "VIP Nổi Bật";
    public bool BannerOn { get; set; } = true;
    public bool ShowAddress { get; set; } = true;

    public string FormattedLocation =>
        string.Join(", ", new[] { District, City }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string FullAddress =>
        string.Join(", ", new[] { Address, Ward, District, City }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string PrimaryImage =>
        Images.Count > 0 ? Images[0] : "/images/p1.png";
}
