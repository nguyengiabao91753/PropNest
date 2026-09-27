using PropNest.Web.Seller.Models.InputModels;
using PropNest.Web.Seller.Models.ViewModels;

namespace PropNest.Web.Seller.Services;

public interface ISellerPropertyService
{
    Task<SellerPropertiesViewModel> GetMyPropertiesAsync(string? status = null, string? searchTerm = null, string layout = "table");
    Task<SellerPropertyItemViewModel?> GetPropertyByIdAsync(string id);
    Task<string> CreatePropertyAsync(CreatePropertyInputModel input);
    Task<bool> UpdatePropertyAsync(string id, CreatePropertyInputModel input);
    Task<bool> SubmitForModerationAsync(string id);
    Task<bool> ToggleHideAsync(string id);
    Task<bool> DeletePropertyAsync(string id);
    Task<List<HistoryDeltaViewModel>> GetHistoryDeltaAsync(string id);
    Task<WalletViewModel> GetWalletAsync();
    Task<bool> TopUpWalletAsync(decimal amount);
    Task<int> GetRejectedNoticesCountAsync();
}
