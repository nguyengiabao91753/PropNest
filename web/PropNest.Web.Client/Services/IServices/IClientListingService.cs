using PropNest.Web.Client.Models.InputModels;
using PropNest.Web.Client.Models.ViewModels;

namespace PropNest.Web.Client.Services.IServices;

public interface IClientListingService
{
    Task<HomeViewModel> GetHomeDataAsync(CancellationToken cancellationToken = default);
    Task<ListingsViewModel> SearchListingsAsync(ListingsViewModel criteria, CancellationToken cancellationToken = default);
    Task<PropertyDetailViewModel?> GetPropertyDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> SendInquiryAsync(InquiryInputModel model, CancellationToken cancellationToken = default);
}
