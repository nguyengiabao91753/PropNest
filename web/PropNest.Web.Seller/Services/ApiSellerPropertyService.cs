using System.Net.Http.Json;
using System.Text.Json;
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
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

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
                    // 1. Thống kê số lượng cho từng Tab Status
                    model.Counts = new StatusCountViewModel
                    {
                        All = response.Items.Count,
                        Published = response.Items.Count(x => x.Status == ListingStatus.Published),
                        PendingModeration = response.Items.Count(x => x.Status == ListingStatus.PendingModeration),
                        Draft = response.Items.Count(x => x.Status == ListingStatus.Draft),
                        Rejected = response.Items.Count(x => x.Status == ListingStatus.Rejected),
                        Hidden = response.Items.Count(x => x.Status == ListingStatus.Hidden),
                        Expired = response.Items.Count(x => x.Status == ListingStatus.Expired)
                    };

                    // 2. Chuyển đổi dữ liệu sang SellerPropertyItemViewModel
                    var allItems = response.Items.Select(x => new SellerPropertyItemViewModel
                    {
                        Id = x.ListingId,
                        Title = x.Title,
                        Description = x.Description ?? string.Empty,
                        PropertyType = x.PropertyType,
                        ListingType = x.ListingType,
                        Price = x.Price,
                        Area = x.Area,
                        City = x.City,
                        District = x.District,
                        Ward = x.Ward,
                        Address = x.Address,
                        Status = x.Status,
                        PackageCode = x.PackageCode ?? "Standard",
                        SellerName = x.OwnerName ?? "Người bán",
                        Images = (x.Images != null && x.Images.Count > 0)
                            ? x.Images
                            : new List<string>(),
                        CreatedAt = x.StartDate ?? DateTimeOffset.UtcNow,
                        RowVersion = x.RowVersion != null ? Convert.ToBase64String(x.RowVersion) : string.Empty
                    }).ToList();

                    // 3. Lọc theo Tab được chọn
                    var filtered = allItems.AsEnumerable();
                    if (!string.Equals(model.CurrentStatus, "all", StringComparison.OrdinalIgnoreCase))
                    {
                        filtered = filtered.Where(x => string.Equals(x.Status.ToString(), model.CurrentStatus, StringComparison.OrdinalIgnoreCase));
                    }

                    // 4. Lọc theo từ khóa tìm kiếm
                    if (!string.IsNullOrWhiteSpace(model.SearchTerm))
                    {
                        var term = model.SearchTerm.Trim();
                        filtered = filtered.Where(x =>
                            x.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            x.Address.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            x.District.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            x.City.Contains(term, StringComparison.OrdinalIgnoreCase));
                    }

                    var filteredList = filtered.ToList();
                    model.TotalCount = filteredList.Count;

                    // 5. Áp dụng Phân trang (Pagination)
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
            var url = $"/api/v1/listings/{id}/hide";
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
                if (dto == null) return null;

                return new SellerPropertyItemViewModel
                {
                    Id = dto.ListingId,
                    Title = dto.Title,
                    Description = dto.Description ?? string.Empty,
                    PropertyType = dto.PropertyType,
                    ListingType = dto.ListingType,
                    Price = dto.Price,
                    Area = dto.Area,
                    City = dto.City,
                    District = dto.District,
                    Ward = dto.Ward,
                    Address = dto.Address,
                    Status = dto.Status,
                    PackageCode = dto.PackageCode ?? "Standard",
                    SellerName = dto.OwnerName ?? "Người bán",
                    Images = (dto.Images != null && dto.Images.Count > 0)
                        ? dto.Images
                        : new List<string>(),
                    CreatedAt = dto.StartDate ?? DateTimeOffset.UtcNow,
                    RowVersion = dto.RowVersion != null ? Convert.ToBase64String(dto.RowVersion) : string.Empty
                };
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return null;
            }
        }

        public Task<List<HistoryDeltaViewModel>> GetHistoryDeltaAsync(long id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<HistoryDeltaViewModel>());
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
                    input.Address
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
                // Upload ảnh nếu có
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
                    input.RowVersion ?? string.Empty
                );
                var response = await _httpClient.PutAsJsonAsync(url, request, _jsonOptions, cancellationToken);
                // Bắt mã 409 Conflict (Optimistic Concurrency)
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
                // Upload thêm ảnh mới nếu có
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

        public async Task<Guid?> PurchasePackageAsync(long listingId, string packageCode, decimal amount, string? idempotencyKey=null, CancellationToken cancellationToken = default)
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
                    return null;
                }
                var dto = await response.Content.ReadFromJsonAsync<ApiWorkflowDto>(_jsonOptions, cancellationToken);
                return dto?.CorrelationId;
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return null;
            }
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
            string District,
            string? Ward,
            string Address
        );
        private sealed record ApiUpdateListingRequest(
            string Title,
            string Description,
            string PropertyType,
            string ListingType,
            decimal Price,
            decimal Area,
            string City,
            string District,
            string? Ward,
            string Address,
            string RowVersion
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
            public string City { get; set; } = string.Empty;
            public string District { get; set; } = string.Empty;
            public string? Ward { get; set; }
            public string Address { get; set; } = string.Empty;
            public ListingStatus Status { get; set; }
            public string? PackageCode { get; set; }
            public DateTimeOffset? StartDate { get; set; }
            public DateTimeOffset? EndDate { get; set; }
            public byte[]? RowVersion { get; set; }
            public string? OwnerName { get; set; }
            public List<string>? Images { get; set; }
        }

        private static string GetFallbackImage(string propertyType) => propertyType switch
        {
            "Apartment" => "https://images.unsplash.com/photo-1545324418-cc1a3fa10c00?auto=format&fit=crop&w=800&q=80",
            "House" => "https://images.unsplash.com/photo-1518780664697-55e3ad937233?auto=format&fit=crop&w=800&q=80",
            "Villa" => "https://images.unsplash.com/photo-1613977257363-707ba9348227?auto=format&fit=crop&w=800&q=80",
            _ => "https://images.unsplash.com/photo-1500382017468-9049fed747ef?auto=format&fit=crop&w=800&q=80"
        };
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
    }
}