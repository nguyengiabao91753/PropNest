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
        private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private readonly JsonSerializerOptions _jsonOptions = s_jsonOptions;

        [LoggerMessage(Level = LogLevel.Warning, Message = "GetHistoryDelta tháº¥t báº¡i cho tin Ä‘Äƒng {ListingId}. Status: {StatusCode}, Chi tiáº¿t: {Details}")]
        private static partial void LogGetHistoryDeltaFailed(ILogger logger, long listingId, System.Net.HttpStatusCode statusCode, string details);

        [LoggerMessage(Level = LogLevel.Error, Message = "Không thể kết nối tới Backend API tại {Url}")]
        private static partial void LogApiConnectionError(ILogger logger, Exception ex, string url);

        [LoggerMessage(Level = LogLevel.Error, Message = "UploadPropertyImages thất bại cho tin đăng {ListingId}. Status: {StatusCode}, Chi tiết: {Details}")]
        private static partial void LogUploadImagesFailed(ILogger logger, long listingId, System.Net.HttpStatusCode statusCode, string details);

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
                // Sắp xếp thời gian mới nhất lên đầu
                return result.OrderBy(x => x.ActionDate).ToList();
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return new List<HistoryDeltaViewModel>();
            }
        }

        public Task<WalletViewModel> GetWalletAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WalletViewModel());
        }

        public Task<bool> TopUpWalletAsync(decimal amount, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
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

        private static string NormalizeActionType(string actionType) => actionType switch
        {
            "Create" => "Tạo mới tin",
            "UpdateContent" or "Update" => "Cập nhật thông tin",
            "Submit" => "Gửi duyệt",
            "Approve" => "Đã duyệt",
            "Reject" => "Từ chối duyệt",
            "Expire" => "Hết hạn",
            "Hide" => "Tạm ẩn",
            "UpgradePackage" => "Nâng cấp gói",
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
            // Nếu là Reject và Note lưu dạng JSON array: ["Lý do 1", "Lý do 2"]
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
                    // fallback nếu không parse được json
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
                // Bỏ qua nếu json lỗi
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
            "City" => "Tỉnh / Thành phố",
            "District" => "Quận / Huyện",
            "Ward" => "Phường / Xã",
            "Address" => "Địa chỉ cụ thể",
            "PackageCode" => "Gói dịch vụ",
            _ => fieldName
        };
        private static string FormatFieldValue(string fieldName, object? value)
        {
            if (value == null) return "(Trống)";
            var strValue = value.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(strValue)) return "(Trống)";
            // Format tiền VND
            if (string.Equals(fieldName, "Price", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(strValue, out var price))
            {
                return $"{price:N0} ₫";
            }
            // Format diện tích m²
            if (string.Equals(fieldName, "Area", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(strValue, out var area))
            {
                return $"{area:N1} m²";
            }
            // Format hình thức
            if (string.Equals(fieldName, "ListingType", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(strValue, "sale", StringComparison.OrdinalIgnoreCase) ? "Cần bán" : "Cho thuê";
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