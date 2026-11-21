namespace PropNest.Web.Client.Models.ViewModels;

public sealed class ListingsViewModel
{
    // Filter criteria
    public string? Query { get; set; }
    public string Type { get; set; } = "all"; // 'all' | 'sale' | 'rent'
    public string? PropertyType { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? MinArea { get; set; }
    public decimal? MaxArea { get; set; }
    public int? MinBedrooms { get; set; }
    public int? MinBathrooms { get; set; }
    public string? BalconyDirection { get; set; }
    public string? Ward { get; set; }
    public int? ProvinceCode { get; set; }
    public int? DistrictCode { get; set; }
    public int? WardCode { get; set; }
    public string SortBy { get; set; } = "default";
    public string Layout { get; set; } = "grid"; // 'grid' | 'list'
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    // Result items
    public List<ClientPropertyItemViewModel> Properties { get; set; } = new();
    public int TotalCount { get; set; }
}
