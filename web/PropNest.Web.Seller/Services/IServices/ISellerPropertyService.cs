using PropNest.Web.Seller.Models.InputModels;
using PropNest.Web.Seller.Models.ViewModels;

namespace PropNest.Web.Seller.Services.IServices;

public interface ISellerPropertyService
{
    Task<SellerPropertiesViewModel> GetMyPropertiesAsync(string? status = null, string? searchTerm = null, string layout = "table", int pageNumber = 1, int pageSize = 8, CancellationToken cancellationToken = default);
    Task<int> GetRejectedNoticesCountAsync(CancellationToken cancellationToken = default);
    Task<SellerPropertyItemViewModel?> GetPropertyByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> SubmitForModerationAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> ToggleHideAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> DeletePropertyAsync(long id, CancellationToken cancellationToken = default);
    Task<long?> CreatePropertyAsync(CreatePropertyInputModel input, CancellationToken cancellationToken = default);
    Task<UpdatePropertyStatus> UpdatePropertyAsync(long id, CreatePropertyInputModel input, CancellationToken cancellationToken = default);
    Task<bool> UploadPropertyImagesAsync(long listingId, List<IFormFile> files, CancellationToken cancellationToken = default);
}
