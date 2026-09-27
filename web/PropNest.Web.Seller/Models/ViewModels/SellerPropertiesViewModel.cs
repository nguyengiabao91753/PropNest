using PropNest.Domain.Listings;

namespace PropNest.Web.Seller.Models.ViewModels;

public sealed class StatusCountViewModel
{
    public int All { get; set; }
    public int Published { get; set; }
    public int PendingModeration { get; set; }
    public int Draft { get; set; }
    public int PendingPayment { get; set; }
    public int Rejected { get; set; }
    public int Hidden { get; set; }
    public int Expired { get; set; }
}

public sealed class SellerPropertiesViewModel
{
    public List<SellerPropertyItemViewModel> Properties { get; set; } = new();
    public StatusCountViewModel Counts { get; set; } = new();
    public string CurrentStatus { get; set; } = "all";
    public string? SearchTerm { get; set; }
    public string Layout { get; set; } = "table"; // 'table' | 'grid'
    public int RejectedCount => Counts.Rejected;
}
