using System.Collections.Concurrent;
using PropNest.Domain.Listings;
using PropNest.Web.Admin.Models.ViewModels;

namespace PropNest.Web.Admin.Services;

public sealed class MockAdminListingService : IAdminListingService
{
    private readonly ConcurrentDictionary<string, PropertyItemViewModel> _listings = new();
    private readonly decimal _totalRevenue = 2_450_000m; // revenue from purchased VIP & Boost packages
    private static readonly string SharedFilePath = Path.Combine(
        Directory.Exists(@"d:\PropNest") ? @"d:\PropNest" : Path.GetTempPath(),
        ".shared_mock_listings.json");
    private static readonly object SyncLock = new();
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public MockAdminListingService()
    {
        InitializeMockData();
        SyncFromSharedFile();
        SaveToSharedFile();
    }

    private void SyncFromSharedFile()
    {
        lock (SyncLock)
        {
            try
            {
                if (!File.Exists(SharedFilePath)) return;
                var json = File.ReadAllText(SharedFilePath);
                var records = System.Text.Json.JsonSerializer.Deserialize<List<SharedPropertyRecord>>(json);
                if (records is null) return;

                foreach (var r in records)
                {
                    if (_listings.TryGetValue(r.Id, out var existing))
                    {
                        existing.Status = r.Status;
                        existing.RejectReason = r.RejectReason;
                        existing.Title = r.Title;
                        existing.Price = r.Price;
                        existing.Address = r.Address;
                        existing.PackageCode = r.PackageCode;
                    }
                    else
                    {
                        _listings[r.Id] = new PropertyItemViewModel
                        {
                            Id = r.Id,
                            Title = r.Title,
                            Description = r.Description,
                            PropertyType = r.PropertyType,
                            ListingType = r.ListingType,
                            Price = r.Price,
                            Area = r.Area,
                            Beds = r.Beds,
                            Baths = r.Baths,
                            City = r.City,
                            District = r.District,
                            Ward = r.Ward,
                            Address = r.Address,
                            Status = r.Status,
                            PackageCode = r.PackageCode,
                            SellerId = r.SellerId,
                            SellerName = r.SellerName,
                            SellerAvatar = r.SellerAvatar ?? "/images/avatar-1.png",
                            Images = r.Images.Count > 0 ? r.Images : new List<string> { "/images/p1.png" },
                            CreatedAt = r.CreatedAt,
                            RejectReason = r.RejectReason,
                            RowVersion = r.RowVersion,
                            Histories = new List<PropertyHistoryItemViewModel>
                            {
                                new() { ActionType = "Create", ActionDate = r.CreatedAt.AddMinutes(-5), Actor = r.SellerName, Note = "Người bán khởi tạo tin đăng" },
                                new() { ActionType = "Submit", ActionDate = r.CreatedAt, Actor = r.SellerName, Note = "Gửi vào hàng đợi duyệt F03" }
                            }
                        };
                    }
                }
            }
            catch { }
        }
    }

    private void SaveToSharedFile()
    {
        lock (SyncLock)
        {
            try
            {
                var records = _listings.Values.Select(item => new SharedPropertyRecord
                {
                    Id = item.Id,
                    Title = item.Title,
                    Description = item.Description,
                    PropertyType = item.PropertyType,
                    ListingType = item.ListingType,
                    Price = item.Price,
                    Area = item.Area,
                    Beds = item.Beds,
                    Baths = item.Baths,
                    City = item.City,
                    District = item.District,
                    Ward = item.Ward,
                    Address = item.Address,
                    Status = item.Status,
                    PackageCode = item.PackageCode,
                    SellerId = item.SellerId,
                    SellerName = item.SellerName,
                    SellerAvatar = item.SellerAvatar,
                    Images = item.Images,
                    CreatedAt = item.CreatedAt,
                    RejectReason = item.RejectReason,
                    RowVersion = item.RowVersion
                }).ToList();

                var json = System.Text.Json.JsonSerializer.Serialize(records, JsonOptions);
                File.WriteAllText(SharedFilePath, json);
            }
            catch { }
        }
    }

    public Task<DashboardViewModel> GetDashboardDataAsync()
    {
        SyncFromSharedFile();
        var all = _listings.Values.OrderByDescending(p => p.CreatedAt).ToList();
        var pendingList = all.Where(p => p.Status == ListingStatus.PendingModeration).ToList();
        var published = all.Count(p => p.Status == ListingStatus.Published);
        var distinctSellers = all.Select(p => p.SellerId).Distinct().Count();

        // Thống kê phân bổ 8 trạng thái vòng đời tin đăng (Blueprint F02)
        var distribution = new Dictionary<string, int>
        {
            ["Published"] = published,
            ["PendingModeration"] = pendingList.Count,
            ["Draft"] = all.Count(p => p.Status == ListingStatus.Draft),
            ["PendingPayment"] = all.Count(p => p.Status == ListingStatus.PendingPayment),
            ["Rejected"] = all.Count(p => p.Status == ListingStatus.Rejected),
            ["Hidden"] = all.Count(p => p.Status == ListingStatus.Hidden),
            ["Expired"] = all.Count(p => p.Status == ListingStatus.Expired)
        };

        // Doanh thu và lượng tin đăng 6 tháng gần nhất (Blueprint F05 & Saga F06)
        var monthlyMetrics = new List<MonthlyMetricItem>
        {
            new() { Month = "T5/2026", Revenue = 18_200_000m, ListingCount = 42, WalletTransactionCount = 58 },
            new() { Month = "T6/2026", Revenue = 24_000_000m, ListingCount = 55, WalletTransactionCount = 76 },
            new() { Month = "T7/2026", Revenue = 31_500_000m, ListingCount = 68, WalletTransactionCount = 94 },
            new() { Month = "T8/2026", Revenue = 42_000_000m, ListingCount = 84, WalletTransactionCount = 118 },
            new() { Month = "T9/2026", Revenue = 58_500_000m, ListingCount = 112, WalletTransactionCount = 145 },
            new() { Month = "T10/2026", Revenue = 72_000_000m, ListingCount = Math.Max(all.Count, 126), WalletTransactionCount = 182 }
        };

        var recentTransactions = new List<WalletTransactionItemViewModel>
        {
            new()
            {
                TransactionId = "TX-9082",
                UserName = "Nguyễn Văn Minh",
                TransactionType = "TopUp",
                Amount = 5_000_000m,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-12),
                Status = "Thành công",
                Description = "Nạp tiền vào ví qua cổng thanh toán VNPay"
            },
            new()
            {
                TransactionId = "TX-9081",
                UserName = "Trần Thị Thu Hà",
                TransactionType = "Charge",
                Amount = 500_000m,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                Status = "Thành công",
                Description = "Thanh toán gói tin VIP cho tin đăng PN-1017"
            },
            new()
            {
                TransactionId = "TX-9080",
                UserName = "Lê Hoàng Long",
                TransactionType = "Charge",
                Amount = 200_000m,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-3),
                Status = "Thành công",
                Description = "Thanh toán gói đẩy tin Boost cho tin đăng PN-1051"
            },
            new()
            {
                TransactionId = "TX-9079",
                UserName = "Phạm Quang Huy",
                TransactionType = "Refund",
                Amount = 500_000m,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-6),
                Status = "Thành công",
                Description = "Bồi hoàn phí Saga do kiểm duyệt từ chối theo chính sách F06"
            },
            new()
            {
                TransactionId = "TX-9078",
                UserName = "Đặng Mai Anh",
                TransactionType = "TopUp",
                Amount = 2_000_000m,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-9),
                Status = "Thành công",
                Description = "Nạp tiền ví điện tử qua mã VietQR"
            }
        };

        var totalUsers = Math.Max(distinctSellers * 18 + 112, 148);
        var totalWalletTx = 342;
        var totalWalletVol = 48_500_000m;

        var model = new DashboardViewModel
        {
            TotalProperties = all.Count,
            PublishedCount = published,
            PendingCount = pendingList.Count,
            TotalUsers = totalUsers,
            TotalWalletTransactions = totalWalletTx,
            TotalWalletVolume = totalWalletVol,
            ActiveSellersCount = distinctSellers,
            TotalRevenue = _totalRevenue,
            RecentProperties = all.Take(6).ToList(),
            PendingQueue = pendingList.Take(5).ToList(),
            RecentTransactions = recentTransactions,
            StatusDistribution = distribution,
            MonthlyMetrics = monthlyMetrics,
            WalletStats = new WalletDistributionViewModel
            {
                TopUpCount = 168,
                PackagePurchaseCount = 142,
                RefundCount = 32,
                TotalTopUpAmount = 32_000_000m,
                TotalSpentAmount = 16_500_000m
            },
            AmeAutoApprovedCount = 26,
            AmeFlaggedCount = 3,
            SagaSuccessRate = 99.8
        };

        return Task.FromResult(model);
    }

    public Task<ModerationQueueViewModel> GetModerationQueueAsync(string? selectedId = null)
    {
        SyncFromSharedFile();
        var pending = _listings.Values
            .Where(p => p.Status == ListingStatus.PendingModeration)
            .OrderBy(p => p.CreatedAt) // Strictly FIFO: oldest pending listings first
            .ToList();

        PropertyItemViewModel? selected = null;
        if (!string.IsNullOrWhiteSpace(selectedId) && _listings.TryGetValue(selectedId, out var found))
        {
            selected = found;
        }
        else
        {
            selected = pending.FirstOrDefault();
        }

        var model = new ModerationQueueViewModel
        {
            PendingProperties = pending,
            SelectedProperty = selected
        };

        return Task.FromResult(model);
    }

    public Task<PropertyListViewModel> GetPropertiesAsync(string? filterStatus = null, string? searchTerm = null)
    {
        SyncFromSharedFile();
        var query = _listings.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filterStatus) && !string.Equals(filterStatus, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<ListingStatus>(filterStatus, true, out var parsedStatus))
            {
                query = query.Where(p => p.Status == parsedStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(p =>
                p.Id.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.SellerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.District.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.City.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var list = query.OrderByDescending(p => p.CreatedAt).ToList();

        var model = new PropertyListViewModel
        {
            Properties = list,
            CurrentFilterStatus = string.IsNullOrWhiteSpace(filterStatus) ? "all" : filterStatus,
            SearchTerm = searchTerm,
            TotalCount = _listings.Count
        };

        return Task.FromResult(model);
    }

    public Task<PropertyItemViewModel?> GetPropertyByIdAsync(string id)
    {
        SyncFromSharedFile();
        _listings.TryGetValue(id, out var found);
        return Task.FromResult(found);
    }

    public Task<bool> ApproveListingAsync(string id)
    {
        if (_listings.TryGetValue(id, out var item))
        {
            item.Status = ListingStatus.Published;
            item.RejectReason = null;
            item.Histories.Add(new PropertyHistoryItemViewModel
            {
                ActionType = "Approve",
                ActionDate = DateTimeOffset.UtcNow,
                Actor = "Kiểm duyệt viên (Moderator)",
                Note = "Tin đăng đã được duyệt và xuất bản công khai"
            });
            SaveToSharedFile();
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> RejectListingAsync(string id, string reason)
    {
        if (_listings.TryGetValue(id, out var item))
        {
            item.Status = ListingStatus.Rejected;
            item.RejectReason = reason;
            item.Histories.Add(new PropertyHistoryItemViewModel
            {
                ActionType = "Reject",
                ActionDate = DateTimeOffset.UtcNow,
                Actor = "Kiểm duyệt viên (Moderator)",
                Note = $"Từ chối duyệt: {reason}"
            });
            SaveToSharedFile();
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<int> GetPendingCountAsync()
    {
        SyncFromSharedFile();
        var count = _listings.Values.Count(p => p.Status == ListingStatus.PendingModeration);
        return Task.FromResult(count);
    }

    private void InitializeMockData()
    {
        var seeds = new List<PropertyItemViewModel>
        {
            new()
            {
                Id = "PN-1000",
                Title = "Biệt Thự Đơn Lập Thảo Điền Ven Sông",
                City = "TP. Hồ Chí Minh",
                District = "Quận 2 (Thủ Đức)",
                Ward = "Thảo Điền",
                Address = "Số 42 Đường Nguyễn Văn Hưởng",
                Price = 35_000_000_000m,
                ListingType = "sale",
                PropertyType = "Villa",
                Beds = 5,
                Baths = 5,
                Area = 450m,
                Status = ListingStatus.Published,
                PackageCode = "VIP",
                SellerId = "seller-1",
                SellerName = "Nguyễn Văn Minh",
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p1.png", "/images/i1.png", "/images/i2.png" },
                Description = "Biệt thự vị trí đắc địa với thiết kế hiện đại, ngập tràn ánh sáng tự nhiên. Pháp lý chuẩn chỉnh, sổ hồng trao tay, công chứng trong ngày. Gần các tiện ích đẳng cấp, bệnh viện quốc tế và trường học danh tiếng.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
            },
            new()
            {
                Id = "PN-1017",
                Title = "Căn Hộ Landmark 81 View Trực Diện Sông",
                City = "TP. Hồ Chí Minh",
                District = "Bình Thạnh",
                Ward = "Phường 22 (Vinhomes)",
                Address = "208 Nguyễn Hữu Cảnh",
                Price = 11_500_000_000m,
                ListingType = "sale",
                PropertyType = "Apartment",
                Beds = 3,
                Baths = 2,
                Area = 110m,
                Status = ListingStatus.Published,
                PackageCode = "VIP",
                SellerId = "seller-2",
                SellerName = "Trần Thị Thu Hà",
                SellerAvatar = "/images/avatar-2.png",
                Images = new List<string> { "/images/p2.png", "/images/i2.png", "/images/i3.png" },
                Description = "Căn nhà được hoàn thiện tỉ mỉ bằng vật liệu cao cấp nhập khẩu. Không gian mở thoáng đãng, ban công rộng view panorama cực kỳ yên bình và thoáng mát.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-4)
            },
            new()
            {
                Id = "PN-1034",
                Title = "Nhà Phố Kinh Doanh Phố Cổ Hoàn Kiếm",
                City = "Hà Nội",
                District = "Hoàn Kiếm",
                Ward = "Hàng Trống",
                Address = "Số 15 Phố Nhà Thờ",
                Price = 28_000_000_000m,
                ListingType = "sale",
                PropertyType = "Townhouse",
                Beds = 4,
                Baths = 4,
                Area = 120m,
                Status = ListingStatus.Published,
                PackageCode = "Boost",
                SellerId = "seller-1",
                SellerName = "Nguyễn Văn Minh",
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p3.png", "/images/i1.png" },
                Description = "Tọa lạc trên tuyến phố sầm uất, hạ tầng đồng bộ, an ninh 24/7. Thích hợp vừa ở vừa làm văn phòng công ty hoặc kinh doanh nhà hàng ẩm thực.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-7)
            },
            new()
            {
                Id = "PN-1051",
                Title = "Biệt Thự Vườn Tây Hồ Ban Công Hồ Tây",
                City = "Hà Nội",
                District = "Tây Hồ",
                Ward = "Quảng An",
                Address = "Ngõ 28 Đặng Thai Mai",
                Price = 42_000_000_000m,
                ListingType = "sale",
                PropertyType = "Villa",
                Beds = 4,
                Baths = 4,
                Area = 380m,
                Status = ListingStatus.Published,
                PackageCode = "VIP",
                SellerId = "seller-3",
                SellerName = "Lê Hoàng Long",
                SellerAvatar = "/images/avatar-3.png",
                Images = new List<string> { "/images/p4.png", "/images/i3.png" },
                Description = "Biệt thự sân vườn thoáng mát, không gian sống sinh thái trong lành bậc nhất thủ đô. Nội thất gỗ óc chó cao cấp.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-10)
            },
            new()
            {
                Id = "PN-1068",
                Title = "Penthouse Duplex Phú Mỹ Hưng View Sân Golf",
                City = "TP. Hồ Chí Minh",
                District = "Quận 7",
                Ward = "Tân Phong",
                Address = "Đường Nguyễn Lương Bằng",
                Price = 45_000_000m,
                ListingType = "rent",
                PropertyType = "Apartment",
                Beds = 3,
                Baths = 3,
                Area = 220m,
                Status = ListingStatus.Published,
                PackageCode = "Standard",
                SellerId = "seller-1",
                SellerName = "Nguyễn Văn Minh",
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p5.png", "/images/i1.png" },
                Description = "Penthouse thông tầng đẳng cấp quốc tế, ban công kính bao trọn cảnh quan sân golf và công viên Nam Sài Gòn.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-13)
            },
            new()
            {
                Id = "PN-1085",
                Title = "Shophouse Thương Mại Cầu Giấy Mặt Tiền 8m",
                City = "Hà Nội",
                District = "Cầu Giấy",
                Ward = "Dịch Vọng Hậu",
                Address = "Duy Tân, Cầu Giấy",
                Price = 18_500_000_000m,
                ListingType = "sale",
                PropertyType = "Commercial",
                Beds = 0,
                Baths = 4,
                Area = 180m,
                Status = ListingStatus.Published,
                PackageCode = "Standard",
                SellerId = "seller-2",
                SellerName = "Trần Thị Thu Hà",
                SellerAvatar = "/images/avatar-2.png",
                Images = new List<string> { "/images/p6.png" },
                Description = "Tuyến phố văn phòng công nghệ thông tin tập trung hàng trăm công ty lớn, lượng khách vãng lai tấp nập suốt ngày đêm.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-16)
            },
            new()
            {
                Id = "PN-1102",
                Title = "Căn Hộ Studio Trung Tâm Quận 3 Đầy Đủ Tiện Nghi",
                City = "TP. Hồ Chí Minh",
                District = "Quận 3",
                Ward = "Võ Thị Sáu",
                Address = "Nam Kỳ Khởi Nghĩa",
                Price = 14_000_000m,
                ListingType = "rent",
                PropertyType = "Apartment",
                Beds = 1,
                Baths = 1,
                Area = 45m,
                Status = ListingStatus.PendingModeration,
                PackageCode = "VIP",
                SellerId = "seller-1",
                SellerName = "Nguyễn Văn Minh",
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p1.png", "/images/i2.png" },
                Description = "Căn hộ dịch vụ cao cấp ngay ngã tư Nam Kỳ Khởi Nghĩa và Điện Biên Phủ, đầy đủ nội thất chỉ cần xách vali vào ở.",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
                AmeRecommendation = new AmeRecommendationViewModel
                {
                    ConfidenceScore = 96,
                    RecommendationText = "Khuyên duyệt",
                    BadwordsPassed = true,
                    PriceBenchmarkText = "Chuẩn khu vực (0.98x)",
                    ImageQualityPassed = true
                }
            },
            new()
            {
                Id = "PN-1119",
                Title = "Nhà Phố Liền Kề Khuê Mỹ Tiện Kinh Doanh",
                City = "Đà Nẵng",
                District = "Ngũ Hành Sơn",
                Ward = "Khuê Mỹ",
                Address = "Lê Văn Hiến",
                Price = 6_800_000_000m,
                ListingType = "sale",
                PropertyType = "Townhouse",
                Beds = 3,
                Baths = 3,
                Area = 100m,
                Status = ListingStatus.PendingModeration,
                PackageCode = "Standard",
                SellerId = "seller-1",
                SellerName = "Nguyễn Văn Minh",
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p2.png", "/images/i1.png" },
                Description = "Nhà phố xây 3 tầng kiên cố, đường 10.5m lề 5m rộng rãi, gần bãi biển Non Nước và bệnh viện Phụ Sản - Nhi.",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-5),
                AmeRecommendation = new AmeRecommendationViewModel
                {
                    ConfidenceScore = 91,
                    RecommendationText = "Khuyên duyệt",
                    BadwordsPassed = true,
                    PriceBenchmarkText = "Chuẩn khu vực (1.02x)",
                    ImageQualityPassed = true
                }
            },
            new()
            {
                Id = "PN-1136",
                Title = "Biệt Thự Nghỉ Dưỡng Thạch Thang Kiến Trúc Pháp",
                City = "Đà Nẵng",
                District = "Hải Châu",
                Ward = "Thạch Thang",
                Address = "Bạch Đằng",
                Price = 19_500_000_000m,
                ListingType = "sale",
                PropertyType = "Villa",
                Beds = 4,
                Baths = 3,
                Area = 260m,
                Status = ListingStatus.PendingModeration,
                PackageCode = "VIP",
                SellerId = "seller-1",
                SellerName = "Nguyễn Văn Minh",
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p3.png" },
                Description = "Vị trí kim cương ngay mặt tiền đường ven sông Hàn thơ mộng, kiến trúc tân cổ điển sang trọng.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
            },
            new()
            {
                Id = "PN-1153",
                Title = "Mặt Bằng Văn Phòng Quận 1 Hạng A Cho Thuê (Đã bổ sung bản vẽ & PCCC)",
                City = "TP. Hồ Chí Minh",
                District = "Quận 1",
                Ward = "Bến Thành",
                Address = "Số 33 Lê Duẩn",
                Price = 120_000_000m,
                ListingType = "rent",
                PropertyType = "Commercial",
                Beds = 0,
                Baths = 4,
                Area = 300m,
                Status = ListingStatus.PendingModeration,
                PackageCode = "VIP",
                SellerId = "seller-1",
                SellerName = "Nguyễn Văn Minh",
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p4.png", "/images/i1.png", "/images/i2.png", "/images/i3.png" },
                Description = "Mặt bằng tòa nhà văn phòng tiêu chuẩn quốc tế hạng A ngay mặt tiền Lê Duẩn, quận 1. Quản lý vận hành chuyên nghiệp bởi CBRE.\n\nĐặc điểm nổi bật:\n- Diện tích sàn sử dụng thực tế: 300m² đã hoàn công chuẩn quy hoạch.\n- Hệ thống PCCC tự động nghiệm thu đạt chuẩn thẩm duyệt số 428/PCCC-TPHCM.\n- 04 thang máy tốc độ cao Schindler, điều hòa trung tâm Daikin VRV.\n- Chiều cao trần 2.8m thông thoáng, view trực diện công viên 30/4.\n- Hợp đồng thuê linh hoạt từ 3 - 5 năm, miễn phí 30 ngày fit-out thi công.",
                RejectReason = "Hình ảnh đăng tải ban đầu bị mờ, diện tích ghi không khớp quy hoạch (ghi 350m² thay vì 300m²) và chưa xuất trình giấy chứng nhận PCCC theo quy định F03.",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                Histories = new List<PropertyHistoryItemViewModel>
                {
                    new() { ActionType = "Create", ActionDate = DateTimeOffset.UtcNow.AddDays(-6), Actor = "Nguyễn Văn Minh", Note = "Người bán khởi tạo tin đăng ban đầu" },
                    new() { ActionType = "Submit", ActionDate = DateTimeOffset.UtcNow.AddDays(-6), Actor = "Nguyễn Văn Minh", Note = "Gửi duyệt tin đăng lần đầu" },
                    new() { ActionType = "Reject", ActionDate = DateTimeOffset.UtcNow.AddDays(-5), Actor = "Kiểm duyệt viên (Moderator)", Note = "Hình ảnh đăng tải ban đầu bị mờ, diện tích ghi không khớp quy hoạch (ghi 350m² thay vì 300m²) và chưa xuất trình giấy chứng nhận PCCC theo quy định F03." },
                    new() { ActionType = "UpdateContent", ActionDate = DateTimeOffset.UtcNow.AddHours(-2), Actor = "Nguyễn Văn Minh", Note = "Đã cập nhật diện tích chuẩn 300m², bổ sung thông tin PCCC và tải lên bộ 4 ảnh sắc nét" },
                    new() { ActionType = "Submit", ActionDate = DateTimeOffset.UtcNow.AddHours(-1), Actor = "Nguyễn Văn Minh", Note = "Gửi duyệt lại (Re-submission) sau khi tiếp thu yêu cầu chỉnh sửa của ban kiểm duyệt" }
                },
                ResubmissionDeltas = new List<ListingDeltaViewModel>
                {
                    new() { FieldName = "Title", FieldDisplayName = "Tiêu đề tin đăng", OldValue = "Mặt Bằng Văn Phòng Quận 1 Hạng A Cho Thuê", NewValue = "Mặt Bằng Văn Phòng Quận 1 Hạng A Cho Thuê (Đã bổ sung bản vẽ & PCCC)" },
                    new() { FieldName = "Area", FieldDisplayName = "Diện tích sàn sử dụng", OldValue = "350 m² (Sai lệch so với hồ sơ bàn giao)", NewValue = "300 m² (Đúng theo bản vẽ hoàn công CBRE)" },
                    new() { FieldName = "Address", FieldDisplayName = "Địa chỉ chi tiết", OldValue = "Lê Duẩn", NewValue = "Số 33 Lê Duẩn, Phường Bến Thành, Quận 1" },
                    new() { FieldName = "Description", FieldDisplayName = "Nội dung mô tả & Pháp lý", OldValue = "Mặt bằng tòa nhà văn phòng tiêu chuẩn quốc tế hạng A, quản lý chuyên nghiệp CBRE.", NewValue = "Đã bổ sung đầy đủ nghiệm thu PCCC số 428/PCCC, thông số 4 thang máy Schindler và điều hòa trung tâm VRV." },
                    new() { FieldName = "Images", FieldDisplayName = "Hình ảnh thực tế", OldValue = "1 hình ảnh chất lượng thấp mờ nhòe", NewValue = "4 hình ảnh độ phân giải cao chụp sảnh, sàn văn phòng và tầm nhìn mặt tiền" }
                },
                AmeRecommendation = new AmeRecommendationViewModel
                {
                    ConfidenceScore = 98,
                    RecommendationText = "Khuyên duyệt (Đã khắc phục lỗi)",
                    BadwordsPassed = true,
                    PriceBenchmarkText = "Chuẩn khu vực Quận 1 (1.0x)",
                    ImageQualityPassed = true
                }
            },
            new()
            {
                Id = "PN-1170",
                Title = "Nhà Riêng Dịch Vọng Yên Tĩnh Ngõ Ô Tô",
                City = "Hà Nội",
                District = "Cầu Giấy",
                Ward = "Dịch Vọng",
                Address = "Trần Thái Tông",
                Price = 7_200_000_000m,
                ListingType = "sale",
                PropertyType = "House",
                Beds = 4,
                Baths = 3,
                Area = 65m,
                Status = ListingStatus.Draft,
                PackageCode = "Standard",
                SellerId = "seller-1",
                SellerName = "Nguyễn Văn Minh",
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p5.png" },
                Description = "Nhà dân xây kiên cố 5 tầng còn rất mới, ô tô đỗ cửa ngày đêm, khu phân lô quân đội dân trí cao.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-3)
            },
            new()
            {
                Id = "PN-1187",
                Title = "Căn Hộ D’Edge Thảo Điền Hồ Bơi Đáy Kính",
                City = "TP. Hồ Chí Minh",
                District = "Quận 2 (Thủ Đức)",
                Ward = "Thảo Điền",
                Address = "Nguyễn Văn Hưởng",
                Price = 55_000_000m,
                ListingType = "rent",
                PropertyType = "Apartment",
                Beds = 3,
                Baths = 2,
                Area = 140m,
                Status = ListingStatus.Hidden,
                PackageCode = "Standard",
                SellerId = "seller-1",
                SellerName = "Nguyễn Văn Minh",
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p6.png" },
                Description = "Dự án biểu tượng của CapitaLand tại Thảo Điền với hồ bơi đáy kính lơ lửng trên không trung.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-12)
            },
            new()
            {
                Id = "PN-1204",
                Title = "Nhà Riêng Phú Nhuận Hẻm Xe Hơi 6m",
                City = "TP. Hồ Chí Minh",
                District = "Phú Nhuận",
                Ward = "Phường 2",
                Address = "Phan Xích Long",
                Price = 9_800_000_000m,
                ListingType = "sale",
                PropertyType = "House",
                Beds = 4,
                Baths = 3,
                Area = 80m,
                Status = ListingStatus.Expired,
                PackageCode = "Standard",
                SellerId = "seller-1",
                SellerName = "Nguyễn Văn Minh",
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p1.png" },
                Description = "Khu phố ẩm thực Phan Xích Long sầm uất, hẻm thông bàn cờ, thuận tiện di chuyển sang Quận 1 chỉ 5 phút.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-35)
            }
        };

        foreach (var seed in seeds)
        {
            if (seed.Histories.Count == 0)
            {
                seed.Histories = new List<PropertyHistoryItemViewModel>
                {
                    new() { ActionType = "Create", ActionDate = seed.CreatedAt.AddHours(-1), Actor = seed.SellerName, Note = "Khởi tạo tin đăng bản nháp" },
                    new() { ActionType = "Submit", ActionDate = seed.CreatedAt, Actor = seed.SellerName, Note = "Gửi yêu cầu kiểm duyệt hệ thống" }
                };
            }
            _listings[seed.Id] = seed;
        }
    }
}

public sealed class SharedPropertyRecord
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PropertyType { get; set; } = "Apartment";
    public string ListingType { get; set; } = "sale";
    public decimal Price { get; set; }
    public decimal Area { get; set; }
    public int Beds { get; set; }
    public int Baths { get; set; }
    public int Carports { get; set; } = 1;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string? Ward { get; set; }
    public string Address { get; set; } = string.Empty;
    public ListingStatus Status { get; set; } = ListingStatus.Draft;
    public string PackageCode { get; set; } = "Standard";
    public string SellerId { get; set; } = "seller-1";
    public string SellerName { get; set; } = "Nguyễn Văn Minh";
    public string? SellerAvatar { get; set; }
    public List<string> Images { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? RejectReason { get; set; }
    public string RowVersion { get; set; } = "AAAAAA==";
}
