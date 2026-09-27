using System.Collections.Concurrent;
using PropNest.Domain.Listings;
using PropNest.Web.Client.Models.InputModels;
using PropNest.Web.Client.Models.ViewModels;

namespace PropNest.Web.Client.Services;

public sealed class MockClientListingService : IClientListingService
{
    private readonly ConcurrentDictionary<string, ClientPropertyItemViewModel> _listings = new();
    private readonly List<InquiryInputModel> _inquiries = new();

    public MockClientListingService()
    {
        InitializeMockData();
    }

    public Task<HomeViewModel> GetHomeDataAsync()
    {
        var published = _listings.Values
            .Where(p => p.Status == ListingStatus.Published)
            .OrderByDescending(p => p.PackageCode == "VIP")
            .ThenByDescending(p => p.CreatedAt)
            .ToList();

        var model = new HomeViewModel
        {
            TotalVerifiedCount = published.Count,
            FeaturedResidential = published.Where(p => p.Category == "Residential").Take(6).ToList(),
            FeaturedCommercial = published.Where(p => p.Category == "Commercial").Take(6).ToList(),
            FeaturedApartments = published.Where(p => p.Category == "Apartments").Take(6).ToList()
        };

        return Task.FromResult(model);
    }

    public Task<ListingsViewModel> SearchListingsAsync(ListingsViewModel criteria, List<string>? savedIds = null)
    {
        var query = _listings.Values.Where(p => p.Status == ListingStatus.Published).AsEnumerable();

        // 1. Text Search
        if (!string.IsNullOrWhiteSpace(criteria.Query))
        {
            var term = criteria.Query.Trim();
            query = query.Where(p =>
                p.Id.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.City.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.District.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (p.Ward != null && p.Ward.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                p.Address.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.PropertyType.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        // 2. Listing Type (Sale / Rent)
        if (!string.IsNullOrWhiteSpace(criteria.Type) && !string.Equals(criteria.Type, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => string.Equals(p.ListingType, criteria.Type, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Home Type
        if (!string.IsNullOrWhiteSpace(criteria.HomeType))
        {
            query = query.Where(p => string.Equals(p.PropertyType, criteria.HomeType, StringComparison.OrdinalIgnoreCase));
        }

        // 4. Price range
        if (criteria.MinPrice.HasValue && criteria.MinPrice.Value > 0)
        {
            query = query.Where(p => p.Price >= criteria.MinPrice.Value);
        }
        if (criteria.MaxPrice.HasValue && criteria.MaxPrice.Value > 0)
        {
            query = query.Where(p => p.Price <= criteria.MaxPrice.Value);
        }

        // 5. Beds & Baths
        if (criteria.Beds > 0)
        {
            query = query.Where(p => p.Beds >= criteria.Beds);
        }
        if (criteria.Baths > 0)
        {
            query = query.Where(p => p.Baths >= criteria.Baths);
        }

        // 6. Facilities
        if (criteria.SelectedFacilities.Count > 0)
        {
            query = query.Where(p => criteria.SelectedFacilities.All(fac => p.Facilities.Contains(fac)));
        }

        // 7. Saved Only
        if (criteria.SavedOnly && savedIds != null && savedIds.Count > 0)
        {
            query = query.Where(p => savedIds.Contains(p.Id));
        }

        // 8. Sorting
        query = criteria.Sort switch
        {
            "price-asc" => query.OrderBy(p => p.Price),
            "price-desc" => query.OrderByDescending(p => p.Price),
            "rating" => query.OrderByDescending(p => p.Rating),
            _ => query.OrderByDescending(p => p.PackageCode == "VIP").ThenByDescending(p => p.CreatedAt)
        };

        var resultList = query.ToList();

        criteria.Properties = resultList;
        return Task.FromResult(criteria);
    }

    public Task<PropertyDetailViewModel?> GetPropertyDetailAsync(string id)
    {
        if (_listings.TryGetValue(id, out var found) && found.Status == ListingStatus.Published)
        {
            var related = _listings.Values
                .Where(p => p.Status == ListingStatus.Published && p.Id != id && p.Category == found.Category)
                .Take(3)
                .ToList();

            var model = new PropertyDetailViewModel
            {
                Property = found,
                RelatedProperties = related,
                Inquiry = new InquiryInputModel { ListingId = id }
            };

            return Task.FromResult<PropertyDetailViewModel?>(model);
        }

        return Task.FromResult<PropertyDetailViewModel?>(null);
    }

    public Task<bool> SendInquiryAsync(InquiryInputModel model)
    {
        _inquiries.Add(model);
        return Task.FromResult(true);
    }

    private void InitializeMockData()
    {
        var sampleProperties = new List<ClientPropertyItemViewModel>
        {
            new()
            {
                Id = "PN-1000",
                Title = "Biệt Thự Thảo Điền Ven Sông Sài Gòn Tuyệt Đẹp",
                Description = "Biệt thự kiến trúc Địa Trung Hải đẳng cấp tại khu dân cư cao cấp Thảo Điền, thành phố Thủ Đức. Diện tích khuôn viên 450m² với sân vườn xanh ngát, hồ bơi tràn viền độc bản, garage để được 2 ô tô và đường dạo bộ ven sông. Bàn giao đầy đủ nội thất nhập khẩu từ Ý.",
                PropertyType = "Villa",
                ListingType = "sale",
                Category = "Residential",
                Price = 85_000_000_000m,
                Area = 450m,
                LandSize = 520m,
                Beds = 5,
                Baths = 6,
                Carports = 2,
                City = "TP. Hồ Chí Minh",
                District = "Quận 2 (Thủ Đức)",
                Ward = "Thảo Điền",
                Address = "Số 18 Đường Nguyễn Văn Hưởng",
                ShowAddress = true,
                Status = ListingStatus.Published,
                PackageCode = "VIP",
                Featured = true,
                Banner = "VIP Nổi Bật",
                SellerId = "seller-1",
                SellerName = "Nguyễn Văn Minh",
                SellerAvatar = "/images/avatar-1.png",
                Rating = 4.9,
                Reviews = 24,
                Images = new List<string> { "/images/p1.png", "/images/p2.png", "/images/p3.png", "/images/p4.png" },
                Facilities = new List<string> { "Bể bơi riêng", "Sân vườn", "Gara ô tô", "An ninh 24/7", "Nội thất cao cấp" },
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
                Lat = 10.8031,
                Lng = 106.7324
            },
            new()
            {
                Id = "PN-1001",
                Title = "Căn Hộ Penthouse Empire City Thủ Thiêm View Landmark 81",
                Description = "Căn hộ Penthouse đỉnh cao tháp Linden Residences với tầm nhìn 360 độ trực diện sông Sài Gòn và tòa Landmark 81. Thiết kế thông tầng trần cao 6.5m, ban công rộng 30m² với kính cường lực nguyên khối, hồ jacuzzi thư giãn riêng tư.",
                PropertyType = "Apartment",
                ListingType = "sale",
                Category = "Apartments",
                Price = 42_000_000_000m,
                Area = 280m,
                LandSize = 0m,
                Beds = 4,
                Baths = 4,
                Carports = 2,
                City = "TP. Hồ Chí Minh",
                District = "Quận 2 (Thủ Đức)",
                Ward = "Thủ Thiêm",
                Address = "Khu đô thị mới Thủ Thiêm, Mai Chí Thọ",
                ShowAddress = true,
                Status = ListingStatus.Published,
                PackageCode = "VIP",
                Featured = true,
                Banner = "Penthouse VIP",
                SellerId = "seller-2",
                SellerName = "Trần Thị Thu Thảo",
                SellerAvatar = "/images/avatar-2.png",
                Rating = 5.0,
                Reviews = 19,
                Images = new List<string> { "/images/p2.png", "/images/p1.png", "/images/p3.png", "/images/p5.png" },
                Facilities = new List<string> { "Hồ Jacuzzi", "Thang máy riêng", "Phòng tập Gym", "Bảo vệ 24/7", "View sông" },
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-5),
                Lat = 10.7716,
                Lng = 106.7118
            },
            new()
            {
                Id = "PN-1002",
                Title = "Nhà Phố Liền Kề Shophouse Sala Đại Quang Minh",
                Description = "Shophouse thương mại trục đường huyết mạch Nguyễn Cơ Thạch, khu đô thị kiểu mẫu Sala. Kết cấu 1 hầm, 4 tầng nổi và áp mái, có sẵn ô chờ thang máy tốc độ cao. Rất thích hợp làm trụ sở tập đoàn hoặc showroom bán lẻ cao cấp.",
                PropertyType = "Commercial",
                ListingType = "rent",
                Category = "Commercial",
                Price = 120_000_000m,
                Area = 320m,
                LandSize = 168m,
                Beds = 4,
                Baths = 5,
                Carports = 2,
                City = "TP. Hồ Chí Minh",
                District = "Quận 2 (Thủ Đức)",
                Ward = "An Lợi Đông",
                Address = "Số 74 Đường Nguyễn Cơ Thạch, KĐT Sala",
                ShowAddress = true,
                Status = ListingStatus.Published,
                PackageCode = "VIP",
                Featured = true,
                Banner = "Mặt tiền Sala",
                SellerId = "seller-1",
                SellerName = "Nguyễn Văn Minh",
                SellerAvatar = "/images/avatar-1.png",
                Rating = 4.8,
                Reviews = 12,
                Images = new List<string> { "/images/p3.png", "/images/p4.png", "/images/p5.png" },
                Facilities = new List<string> { "Hầm để xe", "Mặt tiền kinh doanh", "Thang máy", "Hệ thống PCCC" },
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-7),
                Lat = 10.7680,
                Lng = 106.7215
            },
            new()
            {
                Id = "PN-1003",
                Title = "Biệt Thự Đơn Lập Vinhomes Riverside Long Biên Phong Cách Ý",
                Description = "Biệt thự đơn lập hướng Đông Nam tại tiểu khu Venice Vinhomes Riverside. View ngã ba sông nhân tạo êm đềm, thiết kế chuẩn mực phong cách tân cổ điển Venice với nội thất gỗ gõ đỏ và đá Marble Tây Ban Nha tự nhiên.",
                PropertyType = "Villa",
                ListingType = "sale",
                Category = "Residential",
                Price = 68_000_000_000m,
                Area = 380m,
                LandSize = 420m,
                Beds = 5,
                Baths = 5,
                Carports = 2,
                City = "Hà Nội",
                District = "Long Biên",
                Ward = "Phúc Lợi",
                Address = "Đường Hoa Lan 8, Vinhomes Riverside",
                ShowAddress = true,
                Status = ListingStatus.Published,
                PackageCode = "VIP",
                Featured = true,
                Banner = "Góc 2 Mặt Tiền",
                SellerId = "seller-3",
                SellerName = "Hoàng Hải Yến",
                SellerAvatar = "/images/avatar-3.png",
                Rating = 4.9,
                Reviews = 16,
                Images = new List<string> { "/images/p4.png", "/images/p2.png", "/images/p1.png" },
                Facilities = new List<string> { "Bến du thuyền riêng", "Sân golf mini", "Sân vườn", "Hồ nước bao quanh" },
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
                Lat = 21.0478,
                Lng = 105.9084
            },
            new()
            {
                Id = "PN-1004",
                Title = "Căn Hộ Duplex Masteri Centre Point Vinhomes Grand Park",
                Description = "Căn hộ Duplex độc bản 2 tầng tại phân khu Masteri Centre Point, Quận 9. Tầm nhìn mở rộng toàn cảnh đại công viên 36ha ánh sáng và hồ nhân tạo cát trắng. Bàn giao thiết bị vệ sinh Kohler, bếp Bosch sang trọng.",
                PropertyType = "Apartment",
                ListingType = "sale",
                Category = "Apartments",
                Price = 6_800_000_000m,
                Area = 145m,
                LandSize = 0m,
                Beds = 3,
                Baths = 3,
                Carports = 1,
                City = "TP. Hồ Chí Minh",
                District = "Quận 9 (Thủ Đức)",
                Ward = "Long Bình",
                Address = "Nguyễn Xiển, KĐT Vinhomes Grand Park",
                ShowAddress = false,
                Status = ListingStatus.Published,
                PackageCode = "Standard",
                Featured = false,
                SellerId = "seller-1",
                SellerName = "Nguyễn Văn Minh",
                SellerAvatar = "/images/avatar-1.png",
                Rating = 4.7,
                Reviews = 8,
                Images = new List<string> { "/images/p5.png", "/images/p6.png", "/images/p1.png" },
                Facilities = new List<string> { "Công viên 36ha", "Biển hồ nhân tạo", "Hồ bơi vô cực", "Trung tâm Vincom" },
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-12),
                Lat = 10.8448,
                Lng = 106.8402
            },
            new()
            {
                Id = "PN-1005",
                Title = "Nhà Phố Cổ Hà Nội Phố Hàng Bạc Hoàn Kiếm",
                Description = "Nhà mặt phố cổ Hàng Bạc, vị trí kim cương trung tâm quận Hoàn Kiếm, thuận tiện kinh doanh vàng bạc, trang sức hoặc khách sạn boutique 4 sao phục vụ khách quốc tế. Sổ đỏ chính chủ lâu dài 100%.",
                PropertyType = "House",
                ListingType = "sale",
                Category = "Residential",
                Price = 115_000_000_000m,
                Area = 210m,
                LandSize = 85m,
                Beds = 6,
                Baths = 6,
                Carports = 0,
                City = "Hà Nội",
                District = "Hoàn Kiếm",
                Ward = "Hàng Bạc",
                Address = "Số 52 Phố Hàng Bạc",
                ShowAddress = true,
                Status = ListingStatus.Published,
                PackageCode = "VIP",
                Featured = true,
                Banner = "Phố Cổ Kim Cương",
                SellerId = "seller-3",
                SellerName = "Hoàng Hải Yến",
                SellerAvatar = "/images/avatar-3.png",
                Rating = 4.9,
                Reviews = 31,
                Images = new List<string> { "/images/p6.png", "/images/p3.png", "/images/p2.png" },
                Facilities = new List<string> { "Mặt tiền phố cổ", "Kinh doanh sầm uất", "Sổ đỏ vĩnh viễn" },
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-15),
                Lat = 21.0343,
                Lng = 105.8524
            },
            new()
            {
                Id = "PN-1006",
                Title = "Biệt Thự Nghỉ Dưỡng Mặt Biển Sơn Trà Đà Nẵng",
                Description = "Biệt thự biển ven cung đường tỷ đô Hoàng Sa, chân bán đảo Sơn Trà. Bãi cát trắng mịn riêng tư, nội thất gỗ óc chó tự nhiên, sân thượng ngắm trọn vịnh Đà Nẵng và cầu Thuận Phước lung linh ánh đèn ban đêm.",
                PropertyType = "Villa",
                ListingType = "sale",
                Category = "Residential",
                Price = 52_000_000_000m,
                Area = 500m,
                LandSize = 650m,
                Beds = 4,
                Baths = 5,
                Carports = 3,
                City = "Đà Nẵng",
                District = "Sơn Trà",
                Ward = "Thọ Quang",
                Address = "Đường Hoàng Sa, Bán đảo Sơn Trà",
                ShowAddress = true,
                Status = ListingStatus.Published,
                PackageCode = "Standard",
                Featured = false,
                SellerId = "seller-2",
                SellerName = "Trần Thị Thu Thảo",
                SellerAvatar = "/images/avatar-2.png",
                Rating = 4.8,
                Reviews = 15,
                Images = new List<string> { "/images/p1.png", "/images/p5.png", "/images/p4.png" },
                Facilities = new List<string> { "Mặt biển riêng", "Hồ bơi nước mặn", "Phòng xông hơi", "Sân BBQ ngoài trời" },
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-18),
                Lat = 16.0898,
                Lng = 108.2541
            },
            new()
            {
                Id = "PN-1007",
                Title = "Căn Hộ Dịch Vụ Cao Cấp Cho Thuê Quận 1 Trung Tâm",
                Description = "Căn hộ dịch vụ phong cách Indochine Đông Dương sang trọng, gần phố đi bộ Nguyễn Huệ và Nhà hát Thành Phố. Đầy đủ bếp tiện nghi, dịch vụ dọn phòng 3 lần/tuần, điện nước và internet tốc độ cao trọn gói.",
                PropertyType = "Apartment",
                ListingType = "rent",
                Category = "Apartments",
                Price = 28_000_000m,
                Area = 68m,
                LandSize = 0m,
                Beds = 1,
                Baths = 1,
                Carports = 1,
                City = "TP. Hồ Chí Minh",
                District = "Quận 1",
                Ward = "Bến Nghé",
                Address = "Đường Đồng Khởi, Phường Bến Nghé",
                ShowAddress = true,
                Status = ListingStatus.Published,
                PackageCode = "Standard",
                Featured = false,
                SellerId = "seller-1",
                SellerName = "Nguyễn Văn Minh",
                SellerAvatar = "/images/avatar-1.png",
                Rating = 4.6,
                Reviews = 11,
                Images = new List<string> { "/images/p2.png", "/images/p3.png", "/images/p6.png" },
                Facilities = new List<string> { "Dọn phòng hàng tuần", "Nội thất cao cấp", "Trung tâm Quận 1", "An ninh thẻ từ" },
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-20),
                Lat = 10.7769,
                Lng = 106.7009
            },
            new()
            {
                Id = "PN-1008",
                Title = "Tòa Nhà Văn Phòng Mặt Tiền Nguyễn Thị Minh Khai Quận 3",
                Description = "Tòa nhà 2 hầm 8 tầng nổi, tổng diện tích sàn 1.600m² tại trung tâm Quận 3. Trang bị 2 thang máy Mitsubishi tải trọng lớn, máy phát điện dự phòng Cummins 100% công suất, hệ thống PCCC tiêu chuẩn quốc tế đã nghiệm thu.",
                PropertyType = "Commercial",
                ListingType = "rent",
                Category = "Commercial",
                Price = 350_000_000m,
                Area = 1600m,
                LandSize = 250m,
                Beds = 0,
                Baths = 10,
                Carports = 10,
                City = "TP. Hồ Chí Minh",
                District = "Quận 3",
                Ward = "Võ Thị Sáu",
                Address = "Số 122 Nguyễn Thị Minh Khai",
                ShowAddress = true,
                Status = ListingStatus.Published,
                PackageCode = "VIP",
                Featured = true,
                Banner = "Tòa Văn Phòng VIP",
                SellerId = "seller-3",
                SellerName = "Hoàng Hải Yến",
                SellerAvatar = "/images/avatar-3.png",
                Rating = 5.0,
                Reviews = 7,
                Images = new List<string> { "/images/p3.png", "/images/p5.png", "/images/p1.png" },
                Facilities = new List<string> { "2 Tầng hầm ô tô", "2 Thang máy", "Máy phát điện 100%", "Bảo vệ 24/7" },
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-22),
                Lat = 10.7812,
                Lng = 106.6953
            }
        };

        foreach (var item in sampleProperties)
        {
            _listings[item.Id] = item;
        }
    }
}
