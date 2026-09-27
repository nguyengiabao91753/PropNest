using PropNest.Web.Client.Models.InputModels;
using PropNest.Web.Client.Models.ViewModels;

namespace PropNest.Web.Client.Services;

public interface IClientListingService
{
    Task<HomeViewModel> GetHomeDataAsync();
    Task<ListingsViewModel> SearchListingsAsync(ListingsViewModel criteria, List<string>? savedIds = null);
    Task<PropertyDetailViewModel?> GetPropertyDetailAsync(string id);
    Task<bool> SendInquiryAsync(InquiryInputModel model);
}
