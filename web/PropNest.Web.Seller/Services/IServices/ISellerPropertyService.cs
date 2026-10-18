using PropNest.Web.Seller.Models.InputModels;
using PropNest.Web.Seller.Models.ViewModels;

namespace PropNest.Web.Seller.Services.IServices;

public interface ISellerPropertyService
{
    Task<SellerPropertiesViewModel> GetMyPropertiesAsync(string? status = null, string? searchTerm = null, string layout = "table", int pageNumber = 1, int pageSize = 8, CancellationToken cancellationToken = default);
    Task<int> GetRejectedNoticesCountAsync(CancellationToken cancellationToken = default);
    Task<SellerPropertyItemViewModel?> GetPropertyByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> SubmitForModerationAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> ToggleHideAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> DeletePropertyAsync(string id, CancellationToken cancellationToken = default);
}
