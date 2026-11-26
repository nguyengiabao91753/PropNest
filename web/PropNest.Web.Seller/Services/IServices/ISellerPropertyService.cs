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
    Task<List<SellerListingImageItem>> GetPropertyImagesAsync(long listingId, CancellationToken cancellationToken = default);
    Task<SyncImagesApiResult> SyncPropertyImagesAsync(long listingId, string orderManifest, List<IFormFile>? files, string? expectedRowVersion = null, bool deferModeration = false, CancellationToken cancellationToken = default);
    Task<List<HistoryDeltaViewModel>> GetHistoryDeltaAsync(long id, CancellationToken cancellationToken = default);
    Task<PurchasePackageApiResult> PurchasePackageAsync(long listingId, string packageCode, decimal amount, string? idempotencyKey = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SellerPackageDefinitionViewModel>> GetActivePackagesAsync(CancellationToken cancellationToken = default);
    Task<WorkflowStatusViewModel?> GetWorkflowStatusAsync(Guid correlationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SellerProvinceItem>> GetProvincesAsync(string mode = "post", CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SellerDistrictItem>> GetDistrictsByProvinceAsync(int provinceCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SellerWardItem>> GetWardsByProvinceAsync(int provinceCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SellerWardItem>> GetWardsByDistrictAsync(int districtCode, CancellationToken cancellationToken = default);
    Task<SellerLegacyWardMapping?> ConvertLegacyWardAsync(int? legacyWardCode, string? legacyWardName, int? legacyProvinceCode = null, CancellationToken cancellationToken = default);
    Task<SellerParsedAddressResult?> ParseAddressAsync(string rawAddress, CancellationToken cancellationToken = default);
    Task<SellerDescriptionSuggestionResult?> SuggestDescriptionAsync(SellerSuggestDescriptionInput input, CancellationToken cancellationToken = default);
}
