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
            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri(baseUrl);
            }
        }

        public async Task<HomeViewModel> GetHomeDataAsync(CancellationToken cancellationToken = default)
        {
            var model = new HomeViewModel();
            const string url = "/api/v1/listings?pageNumber=1&pageSize=24";

            try
            {
                var response = await _httpClient.GetFromJsonAsync<ApiPagedResult<ApiListingSummaryDto>>(url, _jsonOptions, cancellationToken);
                if (response is null)
                {
                    return model;
                }

                model.TotalVerifiedCount = response.TotalCount;
                var mapped = response.Items.Select(MapSummaryToViewModel).ToList();

                var residential = mapped
                    .Where(x => string.Equals(x.PropertyType, "House", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(x.PropertyType, "Villa", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(x.PropertyType, "Townhouse", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(x.PropertyType, "Land", StringComparison.OrdinalIgnoreCase))
                    .Take(6)
                    .ToList();

                model.FeaturedResidential = residential.Count > 0 ? residential : mapped.Take(6).ToList();
                model.FeaturedCommercial = mapped
                    .Where(x => string.Equals(x.PropertyType, "Commercial", StringComparison.OrdinalIgnoreCase))
                    .Take(6)
                    .ToList();
                model.FeaturedApartments = mapped
                    .Where(x => string.Equals(x.PropertyType, "Apartment", StringComparison.OrdinalIgnoreCase))
                    .Take(6)
                    .ToList();
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
            }

            return model;
        }

        private static ClientPropertyItemViewModel MapSummaryToViewModel(ApiListingSummaryDto x) => new()
        {
            Id = x.ListingId,
            Title = x.Title,
            Price = x.Price,
            Area = x.Area,
            Floors = x.Floors,
            Bedrooms = x.Bedrooms,
            Bathrooms = x.Bathrooms,
            BalconyDirection = x.BalconyDirection,
            City = x.City,
            District = x.District,
            Ward = x.Ward,
            Address = x.Address,
            AdministrativeMode = x.AdministrativeMode ?? "PostMerger",
            LegacyAddress = x.LegacyAddress,
            PackageCode = x.PackageCode,
            ListingType = x.ListingType,
            PropertyType = x.PropertyType,
            CreatedAt = x.CreatedDate,
            ImageUrls = !string.IsNullOrWhiteSpace(x.ThumbnailUrl)
                ? new List<string> { x.ThumbnailUrl }
                : new List<string>(),
            ThumbnailUrl = x.ThumbnailUrl
        };

        public async Task<PropertyDetailViewModel?> GetPropertyDetailAsync(long id, CancellationToken cancellationToken = default)
        {
            var url = $"/api/v1/listings/{id}";
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }
                response.EnsureSuccessStatusCode();
                var dto = await response.Content.ReadFromJsonAsync<ApiListingDetailDto>(_jsonOptions, cancellationToken);
                if (dto == null) return null;

                var galleryImages = (dto.Images != null && dto.Images.Count > 0)
                    ? dto.Images
                    : GetFallbackGalleryImages(dto.PropertyType);

                return new PropertyDetailViewModel
                {
                    Property = new ClientPropertyItemViewModel
                    {
                        Id = dto.ListingId,
                        Title = dto.Title,
                        Description = dto.Description,
                        Price = dto.Price,
                        Area = dto.Area,
                        Floors = dto.Floors,
                        Bedrooms = dto.Bedrooms,
                        Bathrooms = dto.Bathrooms,
                        BalconyDirection = dto.BalconyDirection,
                        City = dto.City,
                        District = dto.District,
                        Ward = dto.Ward,
                        Address = dto.Address,
                        AdministrativeMode = dto.AdministrativeMode ?? "PostMerger",
                        LegacyAddress = dto.LegacyAddress,
                        PackageCode = dto.PackageCode,
                        ListingType = dto.ListingType,
                        PropertyType = dto.PropertyType,
                        OwnerName = !string.IsNullOrWhiteSpace(dto.OwnerName) ? dto.OwnerName : "Chủ bất động sản",
                        OwnerPhone = !string.IsNullOrWhiteSpace(dto.OwnerPhoneNumber) ? dto.OwnerPhoneNumber : "0901234567",
                        OwnerEmail = dto.OwnerEmail ?? string.Empty,
                        ImageUrls = galleryImages,
                        CreatedAt = dto.StartDate ?? DateTimeOffset.UtcNow
                    }
                };
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return null;
            }
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

            if (criteria.MinArea.HasValue)
            {
                queryParams.Add($"minArea={criteria.MinArea.Value}");
            }

            if (criteria.MaxArea.HasValue)
            {
                queryParams.Add($"maxArea={criteria.MaxArea.Value}");
            }

            if (criteria.MinBedrooms.HasValue && criteria.MinBedrooms.Value > 0)
            {
                queryParams.Add($"minBedrooms={criteria.MinBedrooms.Value}");
            }

            if (criteria.MinBathrooms.HasValue && criteria.MinBathrooms.Value > 0)
            {
                queryParams.Add($"minBathrooms={criteria.MinBathrooms.Value}");
            }

            if (!string.IsNullOrWhiteSpace(criteria.BalconyDirection))
            {
                queryParams.Add($"balconyDirection={Uri.EscapeDataString(criteria.BalconyDirection)}");
            }

            if (criteria.ProvinceCode.HasValue)
            {
                queryParams.Add($"provinceCode={criteria.ProvinceCode.Value}");
            }

            if (criteria.DistrictCode.HasValue)
            {
                queryParams.Add($"districtCode={criteria.DistrictCode.Value}");
            }

            if (criteria.WardCode.HasValue)
            {
                queryParams.Add($"wardCode={criteria.WardCode.Value}");
            }

            if (!string.IsNullOrWhiteSpace(criteria.Ward))
            {
                queryParams.Add($"ward={Uri.EscapeDataString(criteria.Ward)}");
            }

            if (!string.IsNullOrWhiteSpace(criteria.SortBy))
            {
                queryParams.Add($"sortBy={Uri.EscapeDataString(criteria.SortBy)}");
            }

            queryParams.Add($"pageNumber={criteria.PageNumber}");
            queryParams.Add($"pageSize={criteria.PageSize}");

            var url = "/api/v1/listings?" + string.Join("&", queryParams);

            try
            {
                var response = await _httpClient.GetFromJsonAsync<ApiPagedResult<ApiListingSummaryDto>>(url, _jsonOptions, cancellationToken);
                if (response != null)
                {
                    criteria.TotalCount = response.TotalCount;
                    criteria.Properties = response.Items.Select(MapSummaryToViewModel).ToList();
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
            int ModerationDecision,
            int? Floors = null,
            int? Bedrooms = null,
            int? Bathrooms = null,
            string? BalconyDirection = null,
            string? AdministrativeMode = null,
            string? LegacyAddress = null,
            string? ThumbnailUrl = null,
            int? ProvinceCode = null,
            int? DistrictCode = null,
            int? WardCode = null,
            DateTimeOffset? PackageEndDate = null,
            DateTimeOffset? EndDate = null
            );

        private sealed record ApiListingDetailDto(
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
            int Status,
            int ModerationDecision,
            string PackageCode,
            DateTimeOffset? StartDate,
            DateTimeOffset? EndDate,
            string? OwnerName,
            string? OwnerPhoneNumber,
            string? OwnerEmail,
            List<string>? Images,
            int? Floors = null,
            int? Bedrooms = null,
            int? Bathrooms = null,
            string? BalconyDirection = null,
            string? AdministrativeMode = null,
            string? LegacyAddress = null
        );

        private static List<string> GetFallbackGalleryImages(string propertyType) => propertyType switch
        {
            "Apartment" => new()
            {
                "https://images.unsplash.com/photo-1545324418-cc1a3fa10c00?auto=format&fit=crop&w=1200&q=80",
                "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?auto=format&fit=crop&w=1200&q=80",
                "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=1200&q=80",
                "https://images.unsplash.com/photo-1484154218962-a197022b5858?auto=format&fit=crop&w=1200&q=80"
            },
            "House" => new()
            {
                "https://images.unsplash.com/photo-1518780664697-55e3ad937233?auto=format&fit=crop&w=1200&q=80",
                "https://images.unsplash.com/photo-1600585154340-be6161a56a0c?auto=format&fit=crop&w=1200&q=80",
                "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
                "https://images.unsplash.com/photo-1600607687939-ce8a6c25118c?auto=format&fit=crop&w=1200&q=80"
            },
            "Villa" => new()
            {
                "https://images.unsplash.com/photo-1613977257363-707ba9348227?auto=format&fit=crop&w=1200&q=80",
                "https://images.unsplash.com/photo-1613490493576-7fde63acd811?auto=format&fit=crop&w=1200&q=80",
                "https://images.unsplash.com/photo-1580587771525-78b9dba3b914?auto=format&fit=crop&w=1200&q=80",
                "https://images.unsplash.com/photo-1512917774080-9991f1c4c750?auto=format&fit=crop&w=1200&q=80"
            },
            _ => new()
            {
                "https://images.unsplash.com/photo-1500382017468-9049fed747ef?auto=format&fit=crop&w=1200&q=80",
                "https://images.unsplash.com/photo-1500076656116-558758c991c1?auto=format&fit=crop&w=1200&q=80"
            }
        };
    }
}
