namespace PropNest.Web.Seller.Models.ViewModels;

public sealed class DeltaItemViewModel
{
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

public sealed class HistoryDeltaViewModel
{
    public long HistoryId { get; set; }
    public string ListingId { get; set; } = string.Empty;
    public string ActionType { get; set; } = "Cập nhật";
    public string Actor { get; set; } = "Hệ thống";
    public DateTimeOffset ActionDate { get; set; } = DateTimeOffset.UtcNow;
    public string? Note { get; set; }
    public List<DeltaItemViewModel> DeltaChanges { get; set; } = new();
}
