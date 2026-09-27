namespace PropNest.Web.Admin.Models.ViewModels;

public sealed class PropertyListViewModel
{
    public List<PropertyItemViewModel> Properties { get; set; } = new();
    public string CurrentFilterStatus { get; set; } = "all";
    public string? SearchTerm { get; set; }
    public int TotalCount { get; set; }
}
