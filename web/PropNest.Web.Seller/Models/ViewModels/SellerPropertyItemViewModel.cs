using PropNest.Domain.Listings;

namespace PropNest.Web.Seller.Models.ViewModels;

public sealed record PurchasePackageApiResult(bool Success, Guid? CorrelationId, string? ErrorMessage = null);

public sealed record SellerPackageDefinitionViewModel(
    string PackageCode,
    string Name,
    decimal Price,
    int DurationDays,
    string Kind);

public sealed class SellerPropertyItemViewModel
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PropertyType { get; set; } = "Apartment";
    public string ListingType { get; set; } = "sale"; // 'sale' | 'rent'
    public decimal Price { get; set; }
    public decimal Area { get; set; }
    public int? Floors { get; set; }
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public string? BalconyDirection { get; set; }
    public decimal LandSize { get; set; }
    public int Beds => Bedrooms ?? 0;
    public int Baths => Bathrooms ?? 0;
    public int Carports { get; set; } = 1;
    public string Condition { get; set; } = "Nhà mới đẹp";
    public string City { get; set; } = string.Empty;
    public string? District { get; set; } = string.Empty;
    public string? Ward { get; set; }
    public string Address { get; set; } = string.Empty;
    public string AdministrativeMode { get; set; } = "PostMerger";
    public int? ProvinceCode { get; set; }
    public int? DistrictCode { get; set; }
    public int? WardCode { get; set; }
    public string? LegacyAddress { get; set; }
    public ListingStatus Status { get; set; } = ListingStatus.Draft;
    public ModerationDecision ModerationDecision { get; set; } = ModerationDecision.NotRequested;
    public string PackageCode { get; set; } = ListingPackagePolicy.StandardPackageCode; // 'Standard' | 'VIP'
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public DateTimeOffset? PackageEndDate { get; set; }
    public bool HasActiveVipPackage => ListingPackagePolicy.HasActiveVipPackage(PackageCode, PackageEndDate, DateTimeOffset.UtcNow);
    public bool CanPurchasePackage => ListingPackagePolicy.CanPurchasePackage(Status, ModerationDecision);
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
        string.Join(", ", new[] { Ward, District, City }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string FullAddress =>
        string.Join(", ", new[] { Address, Ward, District, City }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string PrimaryImage =>
        Images.Count > 0 ? Images[0] : string.Empty;
}
