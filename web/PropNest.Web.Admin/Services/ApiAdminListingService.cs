using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PropNest.Domain.Listings;
using PropNest.Web.Admin.Models.ViewModels;
using PropNest.Web.Admin.Services.IServices;

namespace PropNest.Web.Admin.Services;

public sealed partial class ApiAdminListingService : IAdminListingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiAdminListingService> _logger;

    private static readonly JsonSerializerOptions NoEscapeJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ApiAdminListingService(
        HttpClient httpClient,
        ILogger<ApiAdminListingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    [LoggerMessage(EventId = 101, Level = LogLevel.Warning, Message = "Failed to fetch moderation queue. Status code: {StatusCode}")]
    private static partial void LogModerationQueueWarning(ILogger logger, HttpStatusCode statusCode);

    [LoggerMessage(EventId = 102, Level = LogLevel.Error, Message = "Error occurred while fetching moderation queue from API.")]
    private static partial void LogModerationQueueError(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 103, Level = LogLevel.Error, Message = "Error fetching property {Id} from API")]
    private static partial void LogPropertyFetchError(ILogger logger, Exception ex, string id);

    [LoggerMessage(EventId = 104, Level = LogLevel.Error, Message = "Error approving listing {Id} via API")]
    private static partial void LogApproveError(ILogger logger, Exception ex, string id);

    [LoggerMessage(EventId = 105, Level = LogLevel.Error, Message = "Error rejecting listing {Id} via API")]
    private static partial void LogRejectError(ILogger logger, Exception ex, string id);

    [LoggerMessage(EventId = 106, Level = LogLevel.Error, Message = "Error fetching pending count from API")]
    private static partial void LogPendingCountError(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 107, Level = LogLevel.Error, Message = "Error fetching property list from API")]
    private static partial void LogPropertiesFetchError(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 108, Level = LogLevel.Error, Message = "Error fetching dashboard data from API")]
    private static partial void LogDashboardDataError(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 109, Level = LogLevel.Warning, Message = "Could not hydrate history for listing {Id}")]
    private static partial void LogHistoryHydrationWarning(ILogger logger, Exception ex, string id);

    public async Task<ModerationQueueViewModel> GetModerationQueueAsync(string? selectedId = null)
    {
        var model = new ModerationQueueViewModel();

        try
        {
            var response = await _httpClient.GetAsync("/api/v1/moderation/listings");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var itemsElement = root.TryGetProperty("items", out var items) ? items : root;
                if (itemsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in itemsElement.EnumerateArray())
                    {
                        var item = MapListingElementToViewModel(elem);
                        model.PendingProperties.Add(item);
                    }
                }
            }
            else
            {
                LogModerationQueueWarning(_logger, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            LogModerationQueueError(_logger, ex);
        }

        if (model.PendingProperties.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                model.SelectedProperty = model.PendingProperties.FirstOrDefault(p => p.Id == selectedId)
                                          ?? model.PendingProperties[0];
            }
            else
            {
                // FIFO order: pick the first item
                model.SelectedProperty = model.PendingProperties[0];
            }

            // Hydrate histories for selected property
            if (model.SelectedProperty != null)
            {
                await HydrateHistoriesAsync(model.SelectedProperty);
            }
        }

        return model;
    }

    public async Task<PropertyItemViewModel?> GetPropertyByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        try
        {
            // 1. Try moderation endpoint first
            var response = await _httpClient.GetAsync($"/api/v1/moderation/listings/{id}");
            if (!response.IsSuccessStatusCode)
            {
                // Fallback to public listings endpoint
                response = await _httpClient.GetAsync($"/api/v1/listings/{id}");
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var vm = MapListingElementToViewModel(doc.RootElement);

            await HydrateHistoriesAsync(vm);
            return vm;
        }
        catch (Exception ex)
        {
            LogPropertyFetchError(_logger, ex, id);
            return null;
        }
    }

    public async Task<bool> ApproveListingAsync(string id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/moderation/listings/{id}/approve", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            LogApproveError(_logger, ex, id);
            return false;
        }
    }

    public async Task<bool> RejectListingAsync(string id, string reason)
    {
        try
        {
            var cleanReason = Helpers.FormatHelper.CleanRejectionReason(reason);
            var payload = new
            {
                reason = cleanReason,
                reasons = new[] { cleanReason },
                reasonsJson = JsonSerializer.Serialize(new[] { cleanReason }, NoEscapeJsonOptions)
            };

            var response = await _httpClient.PostAsJsonAsync($"/api/v1/moderation/listings/{id}/reject", payload);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            LogRejectError(_logger, ex, id);
            return false;
        }
    }

    public async Task<int> GetPendingCountAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/moderation/listings");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                if (root.TryGetProperty("total", out var total))
                {
                    return total.GetInt32();
                }
                if (root.TryGetProperty("totalCount", out var totalCount))
                {
                    return totalCount.GetInt32();
                }
                if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    return items.GetArrayLength();
                }
            }
        }
        catch (Exception ex)
        {
            LogPendingCountError(_logger, ex);
        }
        return 0;
    }

    public async Task<PropertyListViewModel> GetPropertiesAsync(string? filterStatus = null, string? searchTerm = null)
    {
        var model = new PropertyListViewModel();

        try
        {
            // 1. Try admin moderation all-listings endpoint first
            var response = await _httpClient.GetAsync("/api/v1/moderation/listings/all");
            if (!response.IsSuccessStatusCode)
            {
                // Fallback to public listings
                response = await _httpClient.GetAsync("/api/v1/listings?pageSize=50");
            }

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var itemsElement = root.TryGetProperty("items", out var items) ? items : root;
                if (itemsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in itemsElement.EnumerateArray())
                    {
                        var item = MapListingElementToViewModel(elem);

                        if (!string.IsNullOrWhiteSpace(filterStatus) &&
                            !string.Equals(filterStatus, "all", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(item.Status.ToString(), filterStatus, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(searchTerm) &&
                            !item.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                            !item.Id.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                            !item.Address.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        model.Properties.Add(item);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogPropertiesFetchError(_logger, ex);
        }

        return model;
    }

    public async Task<DashboardViewModel> GetDashboardDataAsync()
    {
        var model = new DashboardViewModel();

        try
        {
            // 1. Fetch real-time dashboard stats from Backend API
            var statsResponse = await _httpClient.GetAsync("/api/v1/moderation/dashboard-stats");
            if (statsResponse.IsSuccessStatusCode)
            {
                var content = await statsResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                model.PublishedCount = GetInt(root, "publishedCount") ?? 0;
                model.PendingCount = GetInt(root, "pendingCount") ?? 0;
                var rejectedCount = GetInt(root, "rejectedCount") ?? 0;
                var draftCount = GetInt(root, "draftCount") ?? 0;
                var hiddenCount = GetInt(root, "hiddenCount") ?? 0;
                var expiredCount = GetInt(root, "expiredCount") ?? 0;
                model.TotalProperties = GetInt(root, "totalProperties") ?? (model.PublishedCount + model.PendingCount + rejectedCount);
                model.TotalUsers = GetInt(root, "totalUsers") ?? 0;
                model.ActiveSellersCount = GetInt(root, "activeSellersCount") ?? 0;
                model.TotalWalletTransactions = GetInt(root, "totalWalletTransactions") ?? 0;
                model.TotalWalletVolume = GetDecimal(root, "totalWalletVolume");

                // Populate Status Distribution
                model.StatusDistribution["Published"] = model.PublishedCount;
                model.StatusDistribution["PendingModeration"] = model.PendingCount;
                model.StatusDistribution["Rejected"] = rejectedCount;
                model.StatusDistribution["Draft"] = draftCount;
                model.StatusDistribution["Hidden"] = hiddenCount;
                model.StatusDistribution["Expired"] = expiredCount;

                // Populate Monthly Metrics
                if (TryGetProp(root, "monthlyMetrics", out var mmElem) && mmElem.ValueKind == JsonValueKind.Array)
                {
                    model.MonthlyMetrics.Clear();
                    foreach (var m in mmElem.EnumerateArray())
                    {
                        model.MonthlyMetrics.Add(new MonthlyMetricItem
                        {
                            Month = GetString(m, "month", "Month") ?? string.Empty,
                            Revenue = GetDecimal(m, "revenue", "Revenue"),
                            ListingCount = GetInt(m, "listingCount", "ListingCount") ?? 0,
                            WalletTransactionCount = GetInt(m, "walletTransactionCount", "WalletTransactionCount") ?? 0
                        });
                    }
                }

                // Populate Recent Transactions from API
                if (TryGetProp(root, "recentTransactions", out var rtElem) && rtElem.ValueKind == JsonValueKind.Array)
                {
                    model.RecentTransactions.Clear();
                    foreach (var elem in rtElem.EnumerateArray())
                    {
                        model.RecentTransactions.Add(new WalletTransactionItemViewModel
                        {
                            TransactionId = GetString(elem, "transactionId") ?? string.Empty,
                            UserName = GetString(elem, "userName") ?? string.Empty,
                            TransactionType = GetString(elem, "transactionType") ?? "TopUp",
                            Amount = GetDecimal(elem, "amount"),
                            CreatedAt = TryGetProp(elem, "createdAt", out var ca) && ca.TryGetDateTimeOffset(out var dto) ? dto : DateTimeOffset.UtcNow,
                            Status = GetString(elem, "status") ?? "Thành công",
                            Description = GetString(elem, "description") ?? string.Empty
                        });
                    }
                }

                // Populate Wallet Stats
                if (TryGetProp(root, "walletStats", out var wsElem) && wsElem.ValueKind == JsonValueKind.Object)
                {
                    model.WalletStats.TopUpCount = GetInt(wsElem, "topUpCount") ?? 0;
                    model.WalletStats.PackagePurchaseCount = GetInt(wsElem, "packagePurchaseCount") ?? 0;
                    model.WalletStats.RefundCount = GetInt(wsElem, "refundCount") ?? 0;
                    model.WalletStats.TotalTopUpAmount = GetDecimal(wsElem, "totalTopUpAmount");
                    model.WalletStats.TotalSpentAmount = GetDecimal(wsElem, "totalSpentAmount");
                }
            }

            // 2. Pending Queue (top 5 for dashboard list)
            var queue = await GetModerationQueueAsync();
            model.PendingQueue = queue.PendingProperties.Take(5).ToList();
            if (model.PendingCount == 0 && queue.TotalPending > 0)
            {
                model.PendingCount = queue.TotalPending;
                model.StatusDistribution["PendingModeration"] = queue.TotalPending;
            }
        }
        catch (Exception ex)
        {
            LogDashboardDataError(_logger, ex);
        }

        return model;
    }

    private async Task HydrateHistoriesAsync(PropertyItemViewModel vm)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/listings/{vm.Id}/history");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    vm.Histories.Clear();
                    foreach (var h in doc.RootElement.EnumerateArray())
                    {
                        var actionType = h.TryGetProperty("actionType", out var at) ? at.GetString() ?? "Update" : "Update";
                        var note = h.TryGetProperty("note", out var n) ? n.GetString() : null;
                        var deltasJson = h.TryGetProperty("deltasJson", out var dj) ? dj.GetString() : null;
                        var actionDate = h.TryGetProperty("actionDate", out var ad) && ad.TryGetDateTimeOffset(out var dto)
                            ? dto
                            : DateTimeOffset.UtcNow;

                        var cleanNote = !string.IsNullOrWhiteSpace(note) ? Helpers.FormatHelper.CleanRejectionReason(note) : note;

                        vm.Histories.Add(new PropertyHistoryItemViewModel
                        {
                            ActionType = actionType,
                            ActionDate = actionDate,
                            Actor = string.Equals(actionType, "Reject", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(actionType, "Approve", StringComparison.OrdinalIgnoreCase) ? "Moderator" : "Người bán",
                            Note = cleanNote,
                            DeltasJson = deltasJson
                        });

                        if (string.Equals(actionType, "Reject", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cleanNote))
                        {
                            vm.RejectReason = cleanNote;
                        }

                        if (!string.IsNullOrWhiteSpace(deltasJson))
                        {
                            try
                            {
                                using var deltaDoc = JsonDocument.Parse(deltasJson);
                                if (deltaDoc.RootElement.ValueKind == JsonValueKind.Object)
                                {
                                    foreach (var prop in deltaDoc.RootElement.EnumerateObject())
                                    {
                                        vm.ResubmissionDeltas.Add(new ListingDeltaViewModel
                                        {
                                            FieldName = prop.Name,
                                            FieldDisplayName = GetFieldDisplayName(prop.Name),
                                            OldValue = null,
                                            NewValue = prop.Value.ToString()
                                        });
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore json parse errors for delta
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogHistoryHydrationWarning(_logger, ex, vm.Id);
        }
    }

    private static PropertyItemViewModel MapListingElementToViewModel(JsonElement elem)
    {
        var vm = new PropertyItemViewModel();

        // Listing Id
        vm.Id = GetString(elem, "listingId", "id", "ListingId", "Id") ?? string.Empty;

        // Title
        vm.Title = GetString(elem, "title", "Title") ?? string.Empty;

        // Description
        vm.Description = GetString(elem, "description", "Description") ?? string.Empty;

        // Property Type & Listing Type
        vm.PropertyType = GetString(elem, "propertyType", "PropertyType") ?? "Apartment";
        vm.ListingType = GetString(elem, "listingType", "ListingType") ?? "sale";

        // Price & Area
        vm.Price = GetDecimal(elem, "price", "Price");
        vm.Area = GetDecimal(elem, "area", "Area");

        // Beds & Baths
        vm.Beds = GetInt(elem, "beds", "Beds") ?? 2;
        vm.Baths = GetInt(elem, "baths", "Baths") ?? 2;

        // Location
        vm.City = GetString(elem, "city", "City") ?? string.Empty;
        vm.District = GetString(elem, "district", "District") ?? string.Empty;
        vm.Ward = GetString(elem, "ward", "Ward");
        vm.Address = GetString(elem, "address", "Address") ?? string.Empty;

        // Status
        if (TryGetProp(elem, "status", out var statusElem) || TryGetProp(elem, "Status", out statusElem))
        {
            if (statusElem.ValueKind == JsonValueKind.Number && statusElem.TryGetInt32(out var statusInt))
            {
                vm.Status = (ListingStatus)statusInt;
            }
            else if (Enum.TryParse<ListingStatus>(statusElem.GetString(), true, out var parsedStatus))
            {
                vm.Status = parsedStatus;
            }
        }

        // Package Code
        vm.PackageCode = GetString(elem, "packageCode", "PackageCode") ?? "Standard";

        // Owner Info
        vm.SellerId = GetString(elem, "ownerUserId", "sellerId", "OwnerUserId", "SellerId") ?? string.Empty;
        var ownerName = GetString(elem, "ownerName", "sellerName", "OwnerName", "SellerName");
        vm.SellerName = !string.IsNullOrWhiteSpace(ownerName) ? ownerName : (!string.IsNullOrWhiteSpace(vm.SellerId) ? $"Người bán #{vm.SellerId}" : "Người bán");
        vm.SellerEmail = GetString(elem, "ownerEmail", "sellerEmail", "OwnerEmail", "SellerEmail") ?? string.Empty;
        vm.SellerPhone = GetString(elem, "ownerPhoneNumber", "sellerPhone", "OwnerPhoneNumber", "SellerPhone") ?? string.Empty;

        // Created At
        if ((TryGetProp(elem, "createdAt", out var createdElem) || TryGetProp(elem, "CreatedAt", out createdElem) ||
             TryGetProp(elem, "createdDate", out createdElem) || TryGetProp(elem, "CreatedDate", out createdElem)) &&
            createdElem.TryGetDateTimeOffset(out var createdDate))
        {
            vm.CreatedAt = createdDate;
        }

        // RowVersion
        vm.RowVersion = GetString(elem, "rowVersion", "RowVersion") ?? string.Empty;

        // Images
        if ((TryGetProp(elem, "images", out var imagesElem) || TryGetProp(elem, "Images", out imagesElem)) &&
            imagesElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var img in imagesElem.EnumerateArray())
            {
                var s = img.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    vm.Images.Add(s);
                }
            }
        }

        return vm;
    }

    private static bool TryGetProp(JsonElement elem, string name, out JsonElement value)
    {
        if (elem.TryGetProperty(name, out value)) return true;
        foreach (var p in elem.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string? GetString(JsonElement elem, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProp(elem, name, out var val))
            {
                if (val.ValueKind == JsonValueKind.String) return val.GetString();
                if (val.ValueKind == JsonValueKind.Number) return val.ToString();
            }
        }
        return null;
    }

    private static decimal GetDecimal(JsonElement elem, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProp(elem, name, out var val))
            {
                if (val.ValueKind == JsonValueKind.Number && val.TryGetDecimal(out var d)) return d;
                if (val.ValueKind == JsonValueKind.String && decimal.TryParse(val.GetString(), out var dp)) return dp;
            }
        }
        return 0m;
    }

    private static int? GetInt(JsonElement elem, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProp(elem, name, out var val))
            {
                if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out var i)) return i;
                if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var ip)) return ip;
            }
        }
        return null;
    }

    private static string GetFieldDisplayName(string fieldName)
    {
        return fieldName.ToLowerInvariant() switch
        {
            "price" => "Giá bán",
            "title" => "Tiêu đề tin",
            "description" => "Mô tả chi tiết",
            "area" => "Diện tích",
            "address" => "Địa chỉ",
            "city" => "Tỉnh / Thành phố",
            "district" => "Quận / Huyện",
            "ward" => "Phường / Xã",
            "propertytype" => "Loại bất động sản",
            "listingtype" => "Hình thức giao dịch",
            _ => fieldName
        };
    }
}
