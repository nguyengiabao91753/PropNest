using PropNest.Web.Client.Models.InputModels;
using PropNest.Web.Client.Models.ViewModels;

namespace PropNest.Web.Client.Services.IServices;

public sealed record ClientLocationItem(int Code, string Name, string? DivisionType = null);

public interface IClientListingService
{
    Task<HomeViewModel> GetHomeDataAsync(CancellationToken cancellationToken = default);
    Task<ListingsViewModel> SearchListingsAsync(ListingsViewModel criteria, CancellationToken cancellationToken = default);
    Task<PropertyDetailViewModel?> GetPropertyDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> SendInquiryAsync(InquiryInputModel model, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientLocationItem>> GetProvincesAsync(string mode = "post", CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientLocationItem>> GetDistrictsByProvinceAsync(int provinceCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientLocationItem>> GetWardsByProvinceAsync(int provinceCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientLocationItem>> GetWardsByDistrictAsync(int districtCode, CancellationToken cancellationToken = default);
}
