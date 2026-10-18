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
                            : new List<string> { GetFallbackImage(x.PropertyType) },
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

        public async Task<bool> SubmitForModerationAsync(string id, CancellationToken cancellationToken = default)
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

        public async Task<bool> ToggleHideAsync(string id, CancellationToken cancellationToken = default)
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

        public async Task<bool> DeletePropertyAsync(string id, CancellationToken cancellationToken = default)
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

        public async Task<SellerPropertyItemViewModel?> GetPropertyByIdAsync(string id, CancellationToken cancellationToken = default)
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
                        : new List<string> { GetFallbackImage(dto.PropertyType) },
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

        public Task<string> CreatePropertyAsync(CreatePropertyInputModel input, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public Task<bool> UpdatePropertyAsync(string id, CreatePropertyInputModel input, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<List<HistoryDeltaViewModel>> GetHistoryDeltaAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<HistoryDeltaViewModel>());
        }

        public Task<WalletViewModel> GetWalletAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WalletViewModel());
        }

        public Task<bool> TopUpWalletAsync(decimal amount, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        // ==========================================
        // DTOs & Helper Methods
        // ==========================================
        private sealed record ApiListingCollectionDto(
            int Total,
            List<ApiListingDto> Items
        );

        private sealed record ApiListingDto(
            long ListingId,
            long OwnerUserId,
            string Title,
            string? Description,
            string PropertyType,
            string ListingType,
            decimal Price,
            decimal Area,
            string City,
            string District,
            string? Ward,
            string Address,
            ListingStatus Status,
            string PackageCode,
            DateTimeOffset? StartDate,
            DateTimeOffset? EndDate,
            byte[]? RowVersion,
            string? OwnerName = null,
            List<string>? Images = null
        );

        private static string GetFallbackImage(string propertyType) => propertyType switch
        {
            "Apartment" => "https://images.unsplash.com/photo-1545324418-cc1a3fa10c00?auto=format&fit=crop&w=800&q=80",
            "House" => "https://images.unsplash.com/photo-1518780664697-55e3ad937233?auto=format&fit=crop&w=800&q=80",
            "Villa" => "https://images.unsplash.com/photo-1613977257363-707ba9348227?auto=format&fit=crop&w=800&q=80",
            _ => "https://images.unsplash.com/photo-1500382017468-9049fed747ef?auto=format&fit=crop&w=800&q=80"
        };
    }
}