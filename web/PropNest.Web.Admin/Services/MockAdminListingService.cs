using System.Collections.Concurrent;
using PropNest.Domain.Listings;
using PropNest.Web.Admin.Models.ViewModels;

namespace PropNest.Web.Admin.Services;

public sealed class MockAdminListingService : IAdminListingService
{
    private readonly ConcurrentDictionary<string, PropertyItemViewModel> _listings = new();
    private readonly decimal _totalRevenue = 2_450_000m; // revenue from purchased VIP & Boost packages

    public MockAdminListingService()
    {
        InitializeMockData();
    }

    public Task<DashboardViewModel> GetDashboardDataAsync()
    {
        var all = _listings.Values.OrderByDescending(p => p.CreatedAt).ToList();
        var pendingList = all.Where(p => p.Status == ListingStatus.PendingModeration).ToList();
        var published = all.Count(p => p.Status == ListingStatus.Published);
        var distinctSellers = all.Select(p => p.SellerId).Distinct().Count();

        // Thống kê phân bổ 8 trạng thái vòng đời tin đăng (Blueprint F02)
        var distribution = new Dictionary<string, int>
        {
            ["Published"] = all.Count(p => p.Status == ListingStatus.Published),
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
            new() { Month = "T4/2026", Revenue = 12500000m, ListingCount = 28 },
            new() { Month = "T5/2026", Revenue = 18200000m, ListingCount = 42 },
            new() { Month = "T6/2026", Revenue = 24000000m, ListingCount = 55 },
            new() { Month = "T7/2026", Revenue = 31500000m, ListingCount = 68 },
            new() { Month = "T8/2026", Revenue = 42000000m, ListingCount = 84 },
            new() { Month = "T9/2026", Revenue = 58500000m, ListingCount = 112 }
        };

        var model = new DashboardViewModel
        {
            TotalProperties = all.Count,
            PublishedCount = published,
            PendingCount = pendingList.Count,
            ActiveSellersCount = distinctSellers,
            TotalRevenue = _totalRevenue,
            RecentProperties = all.Take(6).ToList(),
            PendingQueue = pendingList.Take(5).ToList(),
            StatusDistribution = distribution,
            MonthlyMetrics = monthlyMetrics,
            AmeAutoApprovedCount = 26,
            AmeFlaggedCount = 3,
            SagaSuccessRate = 99.8
        };

        return Task.FromResult(model);
    }

    public Task<ModerationQueueViewModel> GetModerationQueueAsync(string? selectedId = null)
    {
        var pending = _listings.Values
            .Where(p => p.Status == ListingStatus.PendingModeration)
            .OrderByDescending(p => p.CreatedAt)
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
        _listings.TryGetValue(id, out var found);
        return Task.FromResult(found);
    }

    public Task<bool> ApproveListingAsync(string id)
    {
        if (_listings.TryGetValue(id, out var item))
        {
            item.Status = ListingStatus.Published;
            item.RejectReason = null;
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
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<int> GetPendingCountAsync()
    {
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
                Status = ListingStatus.PendingPayment,
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
                Title = "Mặt Bằng Văn Phòng Quận 1 Hạng A Cho Thuê",
                City = "TP. Hồ Chí Minh",
                District = "Quận 1",
                Ward = "Bến Thành",
                Address = "Lê Duẩn",
                Price = 120_000_000m,
                ListingType = "rent",
                PropertyType = "Commercial",
                Beds = 0,
                Baths = 4,
                Area = 300m,
                Status = ListingStatus.Rejected,
                PackageCode = "Standard",
                SellerId = "seller-1",
                SellerName = "Nguyễn Văn Minh",
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p4.png" },
                Description = "Mặt bằng tòa nhà văn phòng tiêu chuẩn quốc tế hạng A, quản lý chuyên nghiệp CBRE.",
                RejectReason = "Hình ảnh đăng tải mờ và sai lệch thông tin diện tích thực tế so với quy hoạch.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-6)
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
            _listings[seed.Id] = seed;
        }
    }
}
