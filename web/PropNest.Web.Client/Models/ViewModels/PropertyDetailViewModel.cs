using PropNest.Web.Client.Models.InputModels;

namespace PropNest.Web.Client.Models.ViewModels;

public sealed class PropertyDetailViewModel
{
    public ClientPropertyItemViewModel Property { get; set; } = new();
    public List<ClientPropertyItemViewModel> RelatedProperties { get; set; } = new();
    public InquiryInputModel Inquiry { get; set; } = new();
}
