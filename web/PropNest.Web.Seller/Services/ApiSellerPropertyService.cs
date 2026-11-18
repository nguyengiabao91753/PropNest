using System.Net.Http.Json;
using System.Text.Json;
using PropNest.Domain.Common;
using PropNest.Domain.Listings;
using PropNest.Web.Seller.Models.InputModels;
using PropNest.Web.Seller.Models.ViewModels;
using PropNest.Web.Seller.Services.IServices;

namespace PropNest.Web.Seller.Services
{
    public partial class ApiSellerPropertyService : ISellerPropertyService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiSellerPropertyService> _logger;
        private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private readonly JsonSerializerOptions _jsonOptions = s_jsonOptions;

        [LoggerMessage(Level = LogLevel.Warning, Message = "GetHistoryDelta thất bại cho tin đăng {ListingId}. Status: {StatusCode}, Chi tiết: {Details}")]
        private static partial void LogGetHistoryDeltaFailed(ILogger logger, long listingId, System.Net.HttpStatusCode statusCode, string details);

        [LoggerMessage(Level = LogLevel.Error, Message = "Không thể kết nối tới Backend API tại {Url}")]
        private static partial void LogApiConnectionError(ILogger logger, Exception ex, string url);

        [LoggerMessage(Level = LogLevel.Error, Message = "UploadPropertyImages thất bại cho tin đăng {ListingId}. Status: {StatusCode}, Chi tiết: {Details}")]
        private static partial void LogUploadImagesFailed(ILogger logger, long listingId, System.Net.HttpStatusCode statusCode, string details);

        [LoggerMessage(Level = LogLevel.Error, Message = "PurchasePackage thất bại cho tin đăng {ListingId}. Status: {StatusCode}, Chi tiết: {Details}")]
        private static partial void LogPurchasePackageFailed(ILogger logger, long listingId, System.Net.HttpStatusCode statusCode, string details);

        [LoggerMessage(Level = LogLevel.Error, Message = "CreateProperty thất bại. Status: {StatusCode}, Chi tiết: {Details}")]
        private static partial void LogCreatePropertyFailed(ILogger logger, System.Net.HttpStatusCode statusCode, string details);

        public ApiSellerPropertyService(HttpClient httpClient, ILogger<ApiSellerPropertyService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            var baseUrl = configuration["ServiceUrls:BackendApi"] ?? "https://localhost:65147";
            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri(baseUrl);
            }
        }

        public async Task<SellerPropertiesViewModel> GetMyPropertiesAsync(
            string? status = null,
            string? searchTerm = null,
            string layout = "table",
            int pageNumber = 1,
            int pageSize = 8,
            CancellationToken cancellationToken = default)
        {
            var validPage = pageNumber < 1 ? 1 : pageNumber;
            var validPageSize = pageSize < 1 ? 8 : pageSize;

            var model = new SellerPropertiesViewModel
            {
                CurrentStatus = string.IsNullOrWhiteSpace(status) ? "all" : status.ToLowerInvariant(),
                SearchTerm = searchTerm,
                Layout = string.IsNullOrWhiteSpace(layout) ? "table" : layout,
                PageNumber = validPage,
                PageSize = validPageSize
            };

            var url = "/api/v1/listings/mine";

            try
            {
                var response = await _httpClient.GetFromJsonAsync<ApiListingCollectionDto>(url, _jsonOptions, cancellationToken);
                if (response?.Items != null)
                {
                    model.Counts = new StatusCountViewModel
                    {
                        All = response.Items.Count,
                        Published = response.Items.Count(x => x.Status == ListingStatus.Published),
                        PendingModeration = response.Items.Count(x => x.Status == ListingStatus.PendingModeration),
                        PendingPayment = response.Items.Count(x => x.Status == ListingStatus.PendingPayment),
                        Draft = response.Items.Count(x => x.Status == ListingStatus.Draft),
                        Rejected = response.Items.Count(x => x.Status == ListingStatus.Rejected),
                        Hidden = response.Items.Count(x => x.Status == ListingStatus.Hidden),
                        Expired = response.Items.Count(x => x.Status == ListingStatus.Expired)
                    };

                    var allItems = response.Items.Select(MapToViewModel).ToList();

                    var filtered = allItems.AsEnumerable();
                    if (!string.Equals(model.CurrentStatus, "all", StringComparison.OrdinalIgnoreCase))
                    {
                        filtered = filtered.Where(x => string.Equals(x.Status.ToString(), model.CurrentStatus, StringComparison.OrdinalIgnoreCase));
                    }

                    if (!string.IsNullOrWhiteSpace(model.SearchTerm))
                    {
                        var term = model.SearchTerm.Trim();
                        filtered = filtered.Where(x =>
                            x.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            x.Address.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            x.District.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            x.City.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrWhiteSpace(x.LegacyAddress) && x.LegacyAddress.Contains(term, StringComparison.OrdinalIgnoreCase)));
                    }

                    var filteredList = filtered.ToList();
                    model.TotalCount = filteredList.Count;

                    model.Properties = filteredList
                        .Skip((validPage - 1) * validPageSize)
                        .Take(validPageSize)
                        .ToList();
                }
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                model.Properties = new List<SellerPropertyItemViewModel>();
            }

            return model;
        }

        public async Task<int> GetRejectedNoticesCountAsync(CancellationToken cancellationToken = default)
        {
            var url = "/api/v1/listings/mine";
            try
            {
                var response = await _httpClient.GetFromJsonAsync<ApiListingCollectionDto>(url, _jsonOptions, cancellationToken);
                return response?.Items.Count(x => x.Status == ListingStatus.Rejected) ?? 0;
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return 0;
            }
        }

        public async Task<bool> SubmitForModerationAsync(long id, CancellationToken cancellationToken = default)
        {
            var url = $"/api/v1/listings/{id}/submit";
            try
            {
                var response = await _httpClient.PostAsync(url, null, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return false;
            }
        }

        public async Task<bool> ToggleHideAsync(long id, CancellationToken cancellationToken = default)
        {
            var listing = await GetPropertyByIdAsync(id, cancellationToken);
            if (listing == null)
            {
                return false;
            }

            var url = listing.Status == ListingStatus.Hidden
                ? $"/api/v1/listings/{id}/unhide"
                : $"/api/v1/listings/{id}/hide";

            try
            {
                var response = await _httpClient.PostAsync(url, null, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return false;
            }
        }

        public async Task<bool> DeletePropertyAsync(long id, CancellationToken cancellationToken = default)
        {
            var url = $"/api/v1/listings/{id}";
            try
            {
                var response = await _httpClient.DeleteAsync(url, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return false;
            }
        }

        public async Task<SellerPropertyItemViewModel?> GetPropertyByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            var url = $"/api/v1/listings/{id}";
            try
            {
                var dto = await _httpClient.GetFromJsonAsync<ApiListingDto>(url, _jsonOptions, cancellationToken);
                return dto == null ? null : MapToViewModel(dto);
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return null;
            }
        }

        public async Task<List<HistoryDeltaViewModel>> GetHistoryDeltaAsync(long id, CancellationToken cancellationToken = default)
        {
            var url = $"/api/v1/listings/{id}/history";
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    LogGetHistoryDeltaFailed(_logger, id, response.StatusCode, errorBody);
                    return new List<HistoryDeltaViewModel>();
                }
                var histories = await response.Content.ReadFromJsonAsync<List<ApiListingHistoryDto>>(_jsonOptions, cancellationToken);
                if (histories == null || histories.Count == 0)
                {
                    return new List<HistoryDeltaViewModel>();
                }
                var result = new List<HistoryDeltaViewModel>();
                foreach (var h in histories)
                {
                    result.Add(new HistoryDeltaViewModel
                    {
                        ListingId = id,
                        ActionType = NormalizeActionType(h.ActionType),
                        Actor = FormatActor(h.ActorName, h.ActorRole),
                        ActionDate = h.ActionDate,
                        Note = FormatNote(h.ActionType, h.Note),
                        DeltaChanges = ParseDeltas(h.DeltasJson)
                    });
                }
                return result.OrderBy(x => x.ActionDate).ToList();
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return new List<HistoryDeltaViewModel>();
            }
        }

        public async Task<long?> CreatePropertyAsync(CreatePropertyInputModel input, CancellationToken cancellationToken = default)
        {
            var url = "/api/v1/listings";
            try
            {
                var request = new ApiCreateListingRequest(
                    input.Title,
                    input.Description,
                    input.PropertyType,
                    input.ListingType,
                    input.Price ?? 0m,
                    input.Area ?? 0m,
                    input.City,
                    input.District,
                    input.Ward,
                    input.Address,
                    input.Floors,
                    input.Bedrooms,
                    input.Bathrooms,
                    input.BalconyDirection,
                    string.IsNullOrWhiteSpace(input.AdministrativeMode) ? "PostMerger" : input.AdministrativeMode,
                    input.ProvinceCode,
                    input.DistrictCode,
                    input.WardCode,
                    input.LegacyAddress
                );
                var response = await _httpClient.PostAsJsonAsync(url, request, _jsonOptions, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    LogCreatePropertyFailed(_logger, response.StatusCode, errorBody);
                    return null;
                }
                var created = await response.Content.ReadFromJsonAsync<ApiListingDto>(_jsonOptions, cancellationToken);
                if (created == null) return null;
                var listingId = created.ListingId;
                if (input.ImageFiles != null && input.ImageFiles.Count > 0)
                {
                    await UploadPropertyImagesAsync(listingId, input.ImageFiles, cancellationToken);
                }
                return listingId;
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return null;
            }
        }

        public async Task<UpdatePropertyStatus> UpdatePropertyAsync(long id, CreatePropertyInputModel input, CancellationToken cancellationToken = default)
        {
            var url = $"/api/v1/listings/{id}";
            try
            {
                var request = new ApiUpdateListingRequest(
                    input.Title,
                    input.Description,
                    input.PropertyType,
                    input.ListingType,
                    input.Price ?? 0m,
                    input.Area ?? 0m,
                    input.City,
                    input.District,
                    input.Ward,
                    input.Address,
                    input.RowVersion ?? string.Empty,
                    input.Floors,
                    input.Bedrooms,
                    input.Bathrooms,
                    input.BalconyDirection,
                    string.IsNullOrWhiteSpace(input.AdministrativeMode) ? "PostMerger" : input.AdministrativeMode,
                    input.ProvinceCode,
                    input.DistrictCode,
                    input.WardCode,
                    input.LegacyAddress
                );
                var response = await _httpClient.PutAsJsonAsync(url, request, _jsonOptions, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    return UpdatePropertyStatus.ConcurrencyConflict;
                }
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return UpdatePropertyStatus.NotFound;
                }
                if (!response.IsSuccessStatusCode)
                {
                    return UpdatePropertyStatus.Failed;
                }
                if (input.ImageFiles != null && input.ImageFiles.Count > 0)
                {
                    await UploadPropertyImagesAsync(id, input.ImageFiles, cancellationToken);
                }
                return UpdatePropertyStatus.Success;
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return UpdatePropertyStatus.Failed;
            }
        }

        public async Task<bool> UploadPropertyImagesAsync(long listingId, List<IFormFile> files, CancellationToken cancellationToken = default)
        {
            var url = $"/api/v1/listings/{listingId}/images";
            try
            {
                using var formData = new MultipartFormDataContent();
                foreach (var file in files)
                {
                    if (file.Length == 0) continue;
                    var streamContent = new StreamContent(file.OpenReadStream());
                    if (!string.IsNullOrWhiteSpace(file.ContentType))
                    {
                        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                    }
                    formData.Add(streamContent, "files", file.FileName);
                }
                var response = await _httpClient.PostAsync(url, formData, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    LogUploadImagesFailed(_logger, listingId, response.StatusCode, errorBody);
                    return false;
                }
                return true;
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return false;
            }
        }

        public async Task<PurchasePackageApiResult> PurchasePackageAsync(long listingId, string packageCode, decimal amount, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        {
            var url = $"/api/v1/listings/{listingId}/purchase-package";
            try
            {
                var key = string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString() : idempotencyKey;
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Idempotency-Key", key);
                request.Content = JsonContent.Create(new ApiPurchasePackageRequest(packageCode, amount));
                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    LogPurchasePackageFailed(_logger, listingId, response.StatusCode, errorBody);
                    return new PurchasePackageApiResult(false, null, ExtractErrorMessage(errorBody));
                }
                var dto = await response.Content.ReadFromJsonAsync<ApiWorkflowDto>(_jsonOptions, cancellationToken);
                return dto?.CorrelationId is Guid cid
                    ? new PurchasePackageApiResult(true, cid)
                    : new PurchasePackageApiResult(false, null, "Không nhận được mã tiến trình từ hệ thống.");
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return new PurchasePackageApiResult(false, null, "Không thể kết nối tới máy chủ thanh toán. Vui lòng thử lại sau.");
            }
        }

        public async Task<IReadOnlyList<SellerPackageDefinitionViewModel>> GetActivePackagesAsync(CancellationToken cancellationToken = default)
        {
            const string url = "/api/v1/packages";
            try
            {
                return await _httpClient.GetFromJsonAsync<List<SellerPackageDefinitionViewModel>>(url, _jsonOptions, cancellationToken) ?? [];
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return [];
            }
        }

        private static string? ExtractErrorMessage(string? body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                        return detail.GetString();
                    if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                        return msg.GetString();
                    if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                        return title.GetString();
                }
            }
            catch
            {
            }
            return null;
        }

        public async Task<WorkflowStatusViewModel?> GetWorkflowStatusAsync(Guid correlationId, CancellationToken cancellationToken = default)
        {
            var url = $"/api/v1/workflows/{correlationId}";
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var dto = await response.Content.ReadFromJsonAsync<ApiWorkflowDto>(_jsonOptions, cancellationToken);
                    if (dto == null) return null;

                    return new WorkflowStatusViewModel
                    {
                        CorrelationId = dto.CorrelationId,
                        ListingId = dto.ListingId,
                        PackageCode = dto.PackageCode,
                        ChargeAmount = dto.ChargeAmount,
                        Status = dto.Status,
                        ErrorMessage = dto.ErrorMessage
                    };
                }
                return null;
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return null;
            }
        }

        public async Task<IReadOnlyList<SellerProvinceItem>> GetProvincesAsync(string mode = "post", CancellationToken cancellationToken = default)
        {
            var url = $"/api/v1/locations/provinces?mode={Uri.EscapeDataString(mode)}";
            try
            {
                return await _httpClient.GetFromJsonAsync<List<SellerProvinceItem>>(url, _jsonOptions, cancellationToken) ?? [];
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return [];
            }
        }

        public async Task<IReadOnlyList<SellerDistrictItem>> GetDistrictsByProvinceAsync(int provinceCode, CancellationToken cancellationToken = default)
        {
            var url = $"/api/v1/locations/provinces/{provinceCode}/districts";
            try
            {
                return await _httpClient.GetFromJsonAsync<List<SellerDistrictItem>>(url, _jsonOptions, cancellationToken) ?? [];
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return [];
            }
        }

        public async Task<IReadOnlyList<SellerWardItem>> GetWardsByProvinceAsync(int provinceCode, CancellationToken cancellationToken = default)
        {
            var url = $"/api/v1/locations/provinces/{provinceCode}/wards";
            try
            {
                return await _httpClient.GetFromJsonAsync<List<SellerWardItem>>(url, _jsonOptions, cancellationToken) ?? [];
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return [];
            }
        }

        public async Task<IReadOnlyList<SellerWardItem>> GetWardsByDistrictAsync(int districtCode, CancellationToken cancellationToken = default)
        {
            var url = $"/api/v1/locations/districts/{districtCode}/wards";
            try
            {
                return await _httpClient.GetFromJsonAsync<List<SellerWardItem>>(url, _jsonOptions, cancellationToken) ?? [];
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return [];
            }
        }

        public async Task<SellerLegacyWardMapping?> ConvertLegacyWardAsync(
            int? legacyWardCode,
            string? legacyWardName,
            int? legacyProvinceCode = null,
            CancellationToken cancellationToken = default)
        {
            var queryParts = new List<string>();
            if (legacyWardCode.HasValue && legacyWardCode > 0)
                queryParts.Add($"legacyWardCode={legacyWardCode.Value}");
            if (!string.IsNullOrWhiteSpace(legacyWardName))
                queryParts.Add($"legacyWardName={Uri.EscapeDataString(legacyWardName)}");
            if (legacyProvinceCode.HasValue && legacyProvinceCode > 0)
                queryParts.Add($"legacyProvinceCode={legacyProvinceCode.Value}");

            var url = "/api/v1/locations/convert-legacy?" + string.Join("&", queryParts);
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadFromJsonAsync<SellerLegacyWardMapping>(_jsonOptions, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return null;
            }
        }

        public async Task<SellerParsedAddressResult?> ParseAddressAsync(string rawAddress, CancellationToken cancellationToken = default)
        {
            const string url = "/api/v1/locations/parse-address";
            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, new { rawAddress }, _jsonOptions, cancellationToken);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadFromJsonAsync<SellerParsedAddressResult>(_jsonOptions, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return null;
            }
        }

        public async Task<SellerDescriptionSuggestionResult?> SuggestDescriptionAsync(SellerSuggestDescriptionInput input, CancellationToken cancellationToken = default)
        {
            const string url = "/api/v1/listings/suggest-description";
            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, input, _jsonOptions, cancellationToken);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadFromJsonAsync<SellerDescriptionSuggestionResult>(_jsonOptions, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return null;
            }
        }

        private static SellerPropertyItemViewModel MapToViewModel(ApiListingDto dto) => new()
        {
            Id = dto.ListingId,
            Title = dto.Title,
            Description = dto.Description ?? string.Empty,
            PropertyType = dto.PropertyType,
            ListingType = dto.ListingType,
            Price = dto.Price,
            Area = dto.Area,
            Floors = dto.Floors,
            Bedrooms = dto.Bedrooms,
            Bathrooms = dto.Bathrooms,
            BalconyDirection = dto.BalconyDirection,
            City = dto.City,
            District = dto.District ?? string.Empty,
            Ward = dto.Ward,
            Address = dto.Address,
            AdministrativeMode = dto.AdministrativeMode ?? "PostMerger",
            ProvinceCode = dto.ProvinceCode,
            DistrictCode = dto.DistrictCode,
            WardCode = dto.WardCode,
            LegacyAddress = dto.LegacyAddress,
            Status = dto.Status,
            ModerationDecision = dto.ModerationDecision,
            PackageCode = dto.PackageCode ?? ListingPackagePolicy.StandardPackageCode,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            PackageEndDate = dto.PackageEndDate,
            SellerName = dto.OwnerName ?? "Người bán",
            Images = (dto.Images != null && dto.Images.Count > 0) ? dto.Images : new List<string>(),
            CreatedAt = dto.StartDate ?? DateTimeOffset.UtcNow,
            RowVersion = dto.RowVersion != null ? Convert.ToBase64String(dto.RowVersion) : string.Empty
        };

        // ==========================================
        // DTOs & Helper Methods
        // ==========================================
        private sealed record ApiCreateListingRequest(
            string Title,
            string Description,
            string PropertyType,
            string ListingType,
            decimal Price,
            decimal Area,
            string City,
            string? District,
            string? Ward,
            string Address,
            int? Floors,
            int? Bedrooms,
            int? Bathrooms,
            string? BalconyDirection,
            string AdministrativeMode,
            int? ProvinceCode,
            int? DistrictCode,
            int? WardCode,
            string? LegacyAddress
        );

        private sealed record ApiUpdateListingRequest(
            string Title,
            string Description,
            string PropertyType,
            string ListingType,
            decimal Price,
            decimal Area,
            string City,
            string? District,
            string? Ward,
            string Address,
            string RowVersion,
            int? Floors,
            int? Bedrooms,
            int? Bathrooms,
            string? BalconyDirection,
            string AdministrativeMode,
            int? ProvinceCode,
            int? DistrictCode,
            int? WardCode,
            string? LegacyAddress
        );

        private sealed class ApiListingCollectionDto
        {
            public int Total { get; set; }
            public List<ApiListingDto> Items { get; set; } = new();
        }

        private sealed class ApiListingDto
        {
            public long ListingId { get; set; }
            public long OwnerUserId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string PropertyType { get; set; } = string.Empty;
            public string ListingType { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public decimal Area { get; set; }
            public int? Floors { get; set; }
            public int? Bedrooms { get; set; }
            public int? Bathrooms { get; set; }
            public string? BalconyDirection { get; set; }
            public string City { get; set; } = string.Empty;
            public string? District { get; set; }
            public string? Ward { get; set; }
            public string Address { get; set; } = string.Empty;
            public string? AdministrativeMode { get; set; }
            public int? ProvinceCode { get; set; }
            public int? DistrictCode { get; set; }
            public int? WardCode { get; set; }
            public string? LegacyAddress { get; set; }
            public ListingStatus Status { get; set; }
            public ModerationDecision ModerationDecision { get; set; }
            public string? PackageCode { get; set; }
            public DateTimeOffset? StartDate { get; set; }
            public DateTimeOffset? EndDate { get; set; }
            public DateTimeOffset? PackageEndDate { get; set; }
            public byte[]? RowVersion { get; set; }
            public string? OwnerName { get; set; }
            public List<string>? Images { get; set; }
        }

        private sealed record ApiPurchasePackageRequest(string PackageCode, decimal ChargeAmount);

        private sealed class ApiWorkflowDto
        {
            public Guid CorrelationId { get; set; }
            public long ListingId { get; set; }
            public long UserId { get; set; }
            public string PackageCode { get; set; } = string.Empty;
            public decimal ChargeAmount { get; set; }
            public int Status { get; set; }
            public int RetryCount { get; set; }
            public string? ErrorMessage { get; set; }
            public DateTimeOffset UpdatedAt { get; set; }
        }

        private static string NormalizeActionType(string actionType) => actionType switch
        {
            "Create" => "Tạo mới tin",
            "UpdateContent" or "Update" => "Cập nhật thông tin",
            "Submit" => "Gửi duyệt",
            "Approve" => "Đã duyệt",
            "Reject" => "Từ chối duyệt",
            "Expire" => "Hết hạn",
            "Hide" => "Tạm ẩn",
            "Unhide" => "Bật hiển thị lại",
            "UpgradePackage" => "Thanh toán / Nâng cấp gói",
            "DowngradePackage" => "Hết hạn VIP (chuyển về Standard)",
            "Delete" => "Xóa tin",
            _ => actionType
        };

        private static string FormatActor(string? actorName, string? actorRole)
        {
            if (string.IsNullOrWhiteSpace(actorName))
            {
                return "Hệ thống tự động";
            }
            var roleLabel = actorRole?.ToLowerInvariant() switch
            {
                "seller" => "Người bán",
                "moderator" => "Kiểm duyệt viên",
                "admin" => "Quản trị viên",
                "system" => "Hệ thống",
                _ => actorRole ?? "Người dùng"
            };
            return $"{actorName} ({roleLabel})";
        }

        private static string? FormatNote(string actionType, string? note)
        {
            if (string.IsNullOrWhiteSpace(note)) return null;
            if (string.Equals(actionType, "Reject", StringComparison.OrdinalIgnoreCase) && note.TrimStart().StartsWith('['))
            {
                try
                {
                    var reasons = JsonSerializer.Deserialize<List<string>>(note, s_jsonOptions);
                    if (reasons != null && reasons.Count > 0)
                    {
                        return string.Join("; ", reasons);
                    }
                }
                catch
                {
                }
            }
            return note;
        }

        private static List<DeltaItemViewModel> ParseDeltas(string? deltasJson)
        {
            var list = new List<DeltaItemViewModel>();
            if (string.IsNullOrWhiteSpace(deltasJson)) return list;
            try
            {
                var deltas = JsonSerializer.Deserialize<List<ApiListingDeltaDto>>(deltasJson, s_jsonOptions);
                if (deltas == null) return list;
                foreach (var d in deltas)
                {
                    list.Add(new DeltaItemViewModel
                    {
                        Field = FormatFieldName(d.FieldName),
                        OldValue = FormatFieldValue(d.FieldName, d.OldValue),
                        NewValue = FormatFieldValue(d.FieldName, d.NewValue)
                    });
                }
            }
            catch
            {
            }
            return list;
        }

        private static string FormatFieldName(string fieldName) => fieldName switch
        {
            "Title" => "Tiêu đề tin",
            "Description" => "Mô tả chi tiết",
            "PropertyType" => "Loại bất động sản",
            "ListingType" => "Hình thức (Bán/Cho thuê)",
            "Price" => "Giá bán/cho thuê",
            "Area" => "Diện tích",
            "Floors" => "Số tầng",
            "Bedrooms" => "Số phòng ngủ",
            "Bathrooms" => "Số toilet / WC",
            "BalconyDirection" => "Hướng ban công",
            "AdministrativeMode" => "Chuẩn địa giới hành chính",
            "City" => "Tỉnh / Thành phố",
            "District" => "Quận / Huyện",
            "Ward" => "Phường / Xã",
            "Address" => "Địa chỉ cụ thể",
            "LegacyAddress" => "Địa chỉ trước sáp nhập",
            "PackageCode" => "Gói dịch vụ",
            _ => fieldName
        };

        private static string FormatFieldValue(string fieldName, object? value)
        {
            if (value == null) return "(Trống)";
            var strValue = value.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(strValue)) return "(Trống)";
            if (string.Equals(fieldName, "Price", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(strValue, out var price))
            {
                return $"{price:N0} ₫";
            }
            if (string.Equals(fieldName, "Area", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(strValue, out var area))
            {
                return $"{area:N1} m²";
            }
            if (string.Equals(fieldName, "ListingType", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(strValue, "sale", StringComparison.OrdinalIgnoreCase) ? "Cần bán" : "Cho thuê";
            }
            if (string.Equals(fieldName, "Description", StringComparison.OrdinalIgnoreCase))
            {
                var plain = RichTextHtmlHelper.ToPlainTextPreview(strValue);
                return string.IsNullOrWhiteSpace(plain) ? "(Trống)" : plain;
            }
            return strValue;
        }

        private sealed class ApiListingHistoryDto
        {
            public string ActionType { get; set; } = string.Empty;
            public DateTimeOffset ActionDate { get; set; }
            public long? ActorUserId { get; set; }
            public string? ActorName { get; set; }
            public string? ActorRole { get; set; }
            public string? SnapshotJson { get; set; }
            public string? DeltasJson { get; set; }
            public string? Note { get; set; }
        }

        private sealed class ApiListingDeltaDto
        {
            public string FieldName { get; set; } = string.Empty;
            public object? OldValue { get; set; }
            public object? NewValue { get; set; }
        }
    }
}
