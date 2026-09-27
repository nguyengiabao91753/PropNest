using PropNest.Domain.Listings;

namespace PropNest.Web.Client.Models.ViewModels;

public sealed class ClientPropertyItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PropertyType { get; set; } = "Apartment";
    public string ListingType { get; set; } = "sale"; // 'sale' | 'rent'
    public string Category { get; set; } = "Residential"; // 'Residential' | 'Commercial' | 'Apartments'
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
    public bool ShowAddress { get; set; } = true;
    public ListingStatus Status { get; set; } = ListingStatus.Published;
    public string PackageCode { get; set; } = "Standard";
    public bool Featured { get; set; }
    public string? Banner { get; set; }
    public string SellerId { get; set; } = "seller-1";
    public string SellerName { get; set; } = "Nguyễn Văn Minh";
    public string SellerAvatar { get; set; } = "/images/avatar-1.png";
    public double Rating { get; set; } = 4.8;
    public int Reviews { get; set; } = 18;
    public List<string> Images { get; set; } = new();
    public List<string> Facilities { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public double Lat { get; set; } = 10.8031;
    public double Lng { get; set; } = 106.7324;

    public string PrimaryImage => Images.Count > 0 ? Images[0] : "/images/p1.png";

    public string FormattedLocation =>
        string.Join(", ", new[] { District, City }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string PublicAddress =>
        ShowAddress
            ? string.Join(", ", new[] { Address, Ward, District, City }.Where(s => !string.IsNullOrWhiteSpace(s)))
            : string.Join(", ", new[] { Ward, District, City }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
