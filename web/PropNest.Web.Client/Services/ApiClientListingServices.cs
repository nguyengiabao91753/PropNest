using PropNest.Web.Client.Models.InputModels;
using PropNest.Web.Client.Models.ViewModels;
using PropNest.Web.Client.Services.IServices;
using System.Text.Json;

namespace PropNest.Web.Client.Services
{
    public partial class ApiClientListingServices : IClientListingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiClientListingServices> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
        [LoggerMessage(Level = LogLevel.Error, Message = "Không thể kết nối tới Backend API tại {Url}")]
        private static partial void LogApiConnectionError(ILogger logger, Exception ex, string url);
        public ApiClientListingServices(HttpClient httpClient, ILogger<ApiClientListingServices> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            var baseUrl = configuration["ServiceUrls:BackendApi"] ?? "https://localhost:65147";
            if(_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri(baseUrl);
            }
        }

        public Task<HomeViewModel> GetHomeDataAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HomeViewModel());
        }

        public Task<PropertyDetailViewModel?> GetPropertyDetailAsync(long id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PropertyDetailViewModel?>(null);
        }

        public async Task<ListingsViewModel> SearchListingsAsync(ListingsViewModel criteria, CancellationToken cancellationToken = default)
        {
            var queryParams = new List<string>();

            if (!string.IsNullOrWhiteSpace(criteria.Query))
            {
                queryParams.Add($"q={Uri.EscapeDataString(criteria.Query)}");
            }

            if (!string.IsNullOrWhiteSpace(criteria.City))
            {
                queryParams.Add($"city={Uri.EscapeDataString(criteria.City)}");
            }

            if (!string.IsNullOrWhiteSpace(criteria.District))
            {
                queryParams.Add($"district={Uri.EscapeDataString(criteria.District)}");
            }

            if (!string.IsNullOrWhiteSpace(criteria.PropertyType))
            {
                queryParams.Add($"propertyType={Uri.EscapeDataString(criteria.PropertyType)}");
            }

            if (!string.IsNullOrWhiteSpace(criteria.Type) && !string.Equals(criteria.Type, "all", StringComparison.OrdinalIgnoreCase))
            {
                var normalizedType = criteria.Type.Trim().ToLowerInvariant() switch
                {
                    "rent" => "Rent",
                    "sale" => "Sale",
                    _ => criteria.Type
                };
                queryParams.Add($"listingType={Uri.EscapeDataString(normalizedType)}");
            }

            if (criteria.MinPrice.HasValue)
            {
                queryParams.Add($"minPrice={criteria.MinPrice.Value}");
            }

            if (criteria.MaxPrice.HasValue)
            {
                queryParams.Add($"maxPrice={criteria.MaxPrice.Value}");
            }

            queryParams.Add($"pageNumber={criteria.PageNumber}");
            queryParams.Add($"pageSize={criteria.PageSize}");

            var url = "/api/v1/listings?" + string.Join("&", queryParams);

            try
            {
                var response = await _httpClient.GetFromJsonAsync<ApiPagedResult<ApiListingSummaryDto>>(url, _jsonOptions, cancellationToken);
                if (response != null)
                {
                    // Cập nhật tổng số tin từ API để phân trang hoạt động đúng
                    criteria.TotalCount = response.TotalCount;
                    // Ánh xạ sang danh sách ViewModel cho View
                    criteria.Properties = response.Items.Select(x => new ClientPropertyItemViewModel
                    {
                        Id = x.ListingId,
                        Title = x.Title,
                        Price = x.Price,
                        Area = x.Area,
                        City = x.City,
                        District = x.District,
                        Address = x.Address,
                        PackageCode = x.PackageCode,
                        ListingType = x.ListingType,
                        PropertyType = x.PropertyType,
                        CreatedAt = x.CreatedDate
                    }).ToList();
                }
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                criteria.Properties = new List<ClientPropertyItemViewModel>();
                criteria.TotalCount = 0;
            }

            return criteria;
        }

        public Task<bool> SendInquiryAsync(InquiryInputModel model, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        private sealed record ApiPagedResult<T>(
            List<T> Items, 
            int TotalCount, 
            int PageNumber, 
            int PageSize
            );
        private sealed record ApiListingSummaryDto(
            long ListingId,
            string Title,
            decimal Price,
            decimal Area,
            string ListingType,
            string PropertyType,
            string City,
            string District,
            string? Ward,
            string PackageCode,
            string Address,
            int Status,
            DateTimeOffset CreatedDate,
            int ModerationDecision
            );
    }
}
