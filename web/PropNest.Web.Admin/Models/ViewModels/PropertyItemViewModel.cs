using PropNest.Domain.Listings;

namespace PropNest.Web.Admin.Models.ViewModels;

public sealed class AmeRecommendationViewModel
{
    public int ConfidenceScore { get; set; } = 96;
    public string RecommendationText { get; set; } = "Khuyên duyệt";
    public bool BadwordsPassed { get; set; } = true;
    public string PriceBenchmarkText { get; set; } = "Chuẩn khu vực (0.98x)";
    public bool ImageQualityPassed { get; set; } = true;
}

public sealed class PropertyItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PropertyType { get; set; } = "Apartment";
    public string ListingType { get; set; } = "sale"; // 'sale' | 'rent'
    public decimal Price { get; set; }
    public decimal Area { get; set; }
    public int Beds { get; set; }
    public int Baths { get; set; }
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string? Ward { get; set; }
    public string Address { get; set; } = string.Empty;
    public ListingStatus Status { get; set; } = ListingStatus.Draft;
    public string PackageCode { get; set; } = "Standard"; // 'Standard' | 'VIP' | 'Boost'
    public string SellerId { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;
    public string? SellerAvatar { get; set; }
    public List<string> Images { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? RejectReason { get; set; }
    public string RowVersion { get; set; } = "AAAAAA==";
    public AmeRecommendationViewModel AmeRecommendation { get; set; } = new();

    public string FormattedLocation =>
        string.Join(", ", new[] { District, City }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string FullAddress =>
        string.Join(", ", new[] { Address, Ward, District, City }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string PrimaryImage =>
        Images.Count > 0 ? Images[0] : "/images/p1.png";
}
