namespace PropNest.Web.Admin.Models.ViewModels;

public sealed class ModerationQueueViewModel
{
    public List<PropertyItemViewModel> PendingProperties { get; set; } = new();
    public PropertyItemViewModel? SelectedProperty { get; set; }
    public int TotalPending => PendingProperties.Count;
}
