using PropNest.Web.Admin.Models.ViewModels;

namespace PropNest.Web.Admin.Services;

public interface IAdminListingService
{
    Task<DashboardViewModel> GetDashboardDataAsync();
    Task<ModerationQueueViewModel> GetModerationQueueAsync(string? selectedId = null);
    Task<PropertyListViewModel> GetPropertiesAsync(string? filterStatus = null, string? searchTerm = null);
    Task<PropertyItemViewModel?> GetPropertyByIdAsync(string id);
    Task<bool> ApproveListingAsync(string id);
    Task<bool> RejectListingAsync(string id, string reason);
    Task<int> GetPendingCountAsync();
}
