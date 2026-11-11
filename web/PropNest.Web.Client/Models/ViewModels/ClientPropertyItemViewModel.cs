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
    public string FormattedLocation =>
        string.Join(", ", new[] { District, City }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
