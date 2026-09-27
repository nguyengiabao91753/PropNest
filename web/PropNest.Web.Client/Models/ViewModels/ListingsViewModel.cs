namespace PropNest.Web.Client.Models.ViewModels;

public sealed class ListingsViewModel
{
    // Filter criteria
    public string? Query { get; set; }
    public string Type { get; set; } = "all"; // 'all' | 'sale' | 'rent'
    public string? HomeType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int Beds { get; set; }
    public int Baths { get; set; }
    public List<string> SelectedFacilities { get; set; } = new();
    public bool SavedOnly { get; set; }
    public string Sort { get; set; } = "newest"; // 'newest' | 'price-asc' | 'price-desc' | 'rating'
    public string Layout { get; set; } = "grid"; // 'grid' | 'list'

    // Result items
    public List<ClientPropertyItemViewModel> Properties { get; set; } = new();
    public int TotalCount => Properties.Count;
}
