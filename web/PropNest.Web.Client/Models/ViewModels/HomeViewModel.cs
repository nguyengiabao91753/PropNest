namespace PropNest.Web.Client.Models.ViewModels;

public sealed class HomeViewModel
{
    public List<ClientPropertyItemViewModel> FeaturedResidential { get; set; } = new();
    public List<ClientPropertyItemViewModel> FeaturedCommercial { get; set; } = new();
    public List<ClientPropertyItemViewModel> FeaturedApartments { get; set; } = new();
    public int TotalVerifiedCount { get; set; }
}
