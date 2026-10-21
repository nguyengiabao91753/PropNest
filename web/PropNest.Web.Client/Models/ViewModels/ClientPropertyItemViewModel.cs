using PropNest.Domain.Listings;

namespace PropNest.Web.Client.Models.ViewModels;

public sealed class ClientPropertyItemViewModel
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Area { get; set; } 
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PackageCode { get; set; } = string.Empty;
    public string ListingType { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string? Description {  get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public string OwnerPhone { get; set; } = string.Empty;

    public List<string> ImageUrls { get; set; } = new();

    // Lấy ảnh đầu tiên trong ImageUrls, nếu chưa có ảnh thì fallback theo PropertyType
    public string PrimaryImage => ImageUrls.FirstOrDefault() ?? PropertyType switch
    {
        "Apartment" => "https://images.unsplash.com/photo-1545324418-cc1a3fa10c00?auto=format&fit=crop&w=800&q=80",
        "House" => "https://images.unsplash.com/photo-1518780664697-55e3ad937233?auto=format&fit=crop&w=800&q=80",
        "Villa" => "https://images.unsplash.com/photo-1613977257363-707ba9348227?auto=format&fit=crop&w=800&q=80",
        "Land" => "https://images.unsplash.com/photo-1500382017468-9049fed747ef?auto=format&fit=crop&w=800&q=80",
        _ => "https://images.unsplash.com/photo-1560518883-ce09059eeffa?auto=format&fit=crop&w=800&q=80"
    };
    public string FormattedLocation =>
        string.Join(", ", new[] { District, City }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
