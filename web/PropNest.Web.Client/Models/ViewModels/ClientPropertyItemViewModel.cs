namespace PropNest.Web.Client.Models.ViewModels;

public sealed class ClientPropertyItemViewModel
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Area { get; set; }
    public int? Floors { get; set; }
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public string? BalconyDirection { get; set; }
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string? Ward { get; set; }
    public string Address { get; set; } = string.Empty;
    public string AdministrativeMode { get; set; } = "PostMerger";
    public string? LegacyAddress { get; set; }
    public string PackageCode { get; set; } = string.Empty;
    public string ListingType { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string? Description { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public string OwnerPhone { get; set; } = string.Empty;

    public List<string> ImageUrls { get; set; } = new();
    public string? ThumbnailUrl { get; set; }
    public string PrimaryImageUrl =>
        !string.IsNullOrWhiteSpace(ThumbnailUrl)
            ? ThumbnailUrl
            : (ImageUrls.Count > 0 && !string.IsNullOrWhiteSpace(ImageUrls[0])
                ? ImageUrls[0]
                : "/images/p1.png");

    public string FormattedLocation =>
        string.Join(", ", new[] { Ward, District, City }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public string FullAddress =>
        string.Join(", ", new[] { Address, Ward, District, City }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
