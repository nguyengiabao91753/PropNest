using System.Collections.Concurrent;
using PropNest.Domain.Listings;
using PropNest.Web.Seller.Helpers;
using PropNest.Web.Seller.Models.InputModels;
using PropNest.Web.Seller.Models.ViewModels;

namespace PropNest.Web.Seller.Services;

public sealed class MockSellerPropertyService : ISellerPropertyService
{
    private readonly ConcurrentDictionary<string, SellerPropertyItemViewModel> _listings = new();
    private readonly ConcurrentDictionary<string, List<HistoryDeltaViewModel>> _histories = new();
    private readonly WalletViewModel _wallet = new();
    private readonly string _currentSellerId = "seller-1";
    private readonly string _currentSellerName = "Nguyễn Văn Minh";

    public MockSellerPropertyService()
    {
        InitializeMockData();
    }

    public Task<SellerPropertiesViewModel> GetMyPropertiesAsync(string? status = null, string? searchTerm = null, string layout = "table")
    {
        var mine = _listings.Values
            .Where(p => p.SellerId == _currentSellerId && p.Status != ListingStatus.Deleted)
            .ToList();

        var counts = new StatusCountViewModel
        {
            All = mine.Count,
            Published = mine.Count(p => p.Status == ListingStatus.Published),
            PendingModeration = mine.Count(p => p.Status == ListingStatus.PendingModeration),
            Draft = mine.Count(p => p.Status == ListingStatus.Draft),
            PendingPayment = mine.Count(p => p.Status == ListingStatus.PendingPayment),
            Rejected = mine.Count(p => p.Status == ListingStatus.Rejected),
            Hidden = mine.Count(p => p.Status == ListingStatus.Hidden),
            Expired = mine.Count(p => p.Status == ListingStatus.Expired)
        };

        var query = mine.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<ListingStatus>(status, true, out var parsedStatus))
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
                p.Address.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.District.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.City.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var list = query.OrderByDescending(p => p.CreatedAt).ToList();

        var model = new SellerPropertiesViewModel
        {
            Properties = list,
            Counts = counts,
            CurrentStatus = string.IsNullOrWhiteSpace(status) ? "all" : status,
            SearchTerm = searchTerm,
            Layout = string.Equals(layout, "grid", StringComparison.OrdinalIgnoreCase) ? "grid" : "table"
        };

        return Task.FromResult(model);
    }

    public Task<SellerPropertyItemViewModel?> GetPropertyByIdAsync(string id)
    {
        _listings.TryGetValue(id, out var found);
        return Task.FromResult(found);
    }

    public Task<string> CreatePropertyAsync(CreatePropertyInputModel input)
    {
        var newId = $"PN-{Random.Shared.Next(2000, 9999)}";
        var status = input.IsDraft ? ListingStatus.Draft : ListingStatus.PendingModeration;

        var item = new SellerPropertyItemViewModel
        {
            Id = newId,
            Title = input.Title,
            Description = input.Description,
            PropertyType = input.PropertyType,
            ListingType = input.ListingType,
            Price = input.Price,
            Area = input.Area,
            LandSize = Math.Round(input.Area * 1.2m),
            Beds = input.Beds,
            Baths = input.Baths,
            Carports = input.Carports,
            City = input.City,
            District = input.District,
            Ward = input.Ward,
            Address = input.Address,
            Status = status,
            PackageCode = input.PackageCode,
            SellerId = _currentSellerId,
            SellerName = _currentSellerName,
            SellerAvatar = "/images/avatar-1.png",
            Images = new List<string> { input.ImageUrl, "/images/i1.png", "/images/i2.png" },
            CreatedAt = DateTimeOffset.UtcNow,
            RowVersion = $"AAAAAA{Random.Shared.Next(100, 999)}A=",
            Banner = input.Banner,
            BannerOn = input.BannerOn,
            ShowAddress = input.ShowAddress
        };

        _listings[newId] = item;

        // Record initial history delta
        var history = new HistoryDeltaViewModel
        {
            HistoryId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ListingId = newId,
            ActionType = input.IsDraft ? "Tạo bản nháp (F02)" : "Tạo & Gửi kiểm duyệt (F02/F03)",
            Actor = _currentSellerName,
            ActionDate = DateTimeOffset.UtcNow,
            Note = input.IsDraft ? "Tin đăng lưu nháp" : "Gửi tin lên hàng đợi thẩm định nội dung",
            DeltaChanges = new List<DeltaItemViewModel>
            {
                new() { Field = "Status", OldValue = null, NewValue = status.ToString() },
                new() { Field = "Price", OldValue = null, NewValue = FormatHelper.FormatPrice(input.Price, input.ListingType) },
                new() { Field = "PackageCode", OldValue = null, NewValue = input.PackageCode }
            }
        };

        _histories[newId] = new List<HistoryDeltaViewModel> { history };

        return Task.FromResult(newId);
    }

    public Task<bool> UpdatePropertyAsync(string id, CreatePropertyInputModel input)
    {
        if (_listings.TryGetValue(id, out var item))
        {
            var oldPrice = item.Price;
            var oldTitle = item.Title;

            item.Title = input.Title;
            item.Description = input.Description;
            item.PropertyType = input.PropertyType;
            item.ListingType = input.ListingType;
            item.Price = input.Price;
            item.Area = input.Area;
            item.Beds = input.Beds;
            item.Baths = input.Baths;
            item.City = input.City;
            item.District = input.District;
            item.Ward = input.Ward;
            item.Address = input.Address;
            item.PackageCode = input.PackageCode;
            item.Banner = input.Banner;
            item.BannerOn = input.BannerOn;
            item.ShowAddress = input.ShowAddress;

            // Blueprint: Modifying material fields moves published listing back to PendingModeration
            if (item.Status == ListingStatus.Published)
            {
                item.Status = ListingStatus.PendingModeration;
            }

            // Record History Delta (F04)
            var deltaList = _histories.GetOrAdd(id, _ => new List<HistoryDeltaViewModel>());
            deltaList.Insert(0, new HistoryDeltaViewModel
            {
                HistoryId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ListingId = id,
                ActionType = "Sửa thông tin tin đăng (F04)",
                Actor = _currentSellerName,
                ActionDate = DateTimeOffset.UtcNow,
                Note = "Cập nhật thông tin chi tiết BĐS",
                DeltaChanges = new List<DeltaItemViewModel>
                {
                    new() { Field = "Title", OldValue = oldTitle, NewValue = input.Title },
                    new() { Field = "Price", OldValue = FormatHelper.FormatPrice(oldPrice, item.ListingType), NewValue = FormatHelper.FormatPrice(input.Price, input.ListingType) }
                }
            });

            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> SubmitForModerationAsync(string id)
    {
        if (_listings.TryGetValue(id, out var item))
        {
            var oldStatus = item.Status;
            item.Status = ListingStatus.PendingModeration;
            item.RejectReason = null;

            var deltaList = _histories.GetOrAdd(id, _ => new List<HistoryDeltaViewModel>());
            deltaList.Insert(0, new HistoryDeltaViewModel
            {
                HistoryId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ListingId = id,
                ActionType = "Gửi thẩm định (Submit Moderation)",
                Actor = _currentSellerName,
                ActionDate = DateTimeOffset.UtcNow,
                Note = "Người bán gửi duyệt lại tin đăng",
                DeltaChanges = new List<DeltaItemViewModel>
                {
                    new() { Field = "Status", OldValue = oldStatus.ToString(), NewValue = ListingStatus.PendingModeration.ToString() }
                }
            });

            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> ToggleHideAsync(string id)
    {
        if (_listings.TryGetValue(id, out var item))
        {
            var oldStatus = item.Status;
            item.Status = item.Status == ListingStatus.Published ? ListingStatus.Hidden : ListingStatus.Published;

            var deltaList = _histories.GetOrAdd(id, _ => new List<HistoryDeltaViewModel>());
            deltaList.Insert(0, new HistoryDeltaViewModel
            {
                HistoryId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ListingId = id,
                ActionType = item.Status == ListingStatus.Hidden ? "Tạm ẩn tin đăng" : "Bật hiển thị tin đăng",
                Actor = _currentSellerName,
                ActionDate = DateTimeOffset.UtcNow,
                Note = item.Status == ListingStatus.Hidden ? "Người bán tạm ẩn khỏi tìm kiếm" : "Khôi phục hiển thị công khai",
                DeltaChanges = new List<DeltaItemViewModel>
                {
                    new() { Field = "Status", OldValue = oldStatus.ToString(), NewValue = item.Status.ToString() }
                }
            });

            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> DeletePropertyAsync(string id)
    {
        if (_listings.TryGetValue(id, out var item))
        {
            item.Status = ListingStatus.Deleted;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<List<HistoryDeltaViewModel>> GetHistoryDeltaAsync(string id)
    {
        if (_histories.TryGetValue(id, out var list))
        {
            return Task.FromResult(list);
        }
        return Task.FromResult(new List<HistoryDeltaViewModel>());
    }

    public Task<WalletViewModel> GetWalletAsync()
    {
        return Task.FromResult(_wallet);
    }

    public Task<bool> TopUpWalletAsync(decimal amount)
    {
        if (amount <= 0) return Task.FromResult(false);

        _wallet.MainBalance += amount;
        _wallet.RowVersion = $"0x{Random.Shared.Next(1000, 9999):X8}";

        _wallet.Transactions.Insert(0, new WalletTransactionViewModel
        {
            Id = $"TX-{Random.Shared.Next(10000, 99999)}",
            Type = "TopUp",
            Amount = amount,
            BalanceAfter = _wallet.TotalBalance,
            Description = $"Nạp tiền qua Cổng thanh toán trực tuyến ({FormatHelper.FormatVnd(amount)})",
            CreatedAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid().ToString()
        });

        return Task.FromResult(true);
    }

    public Task<int> GetRejectedNoticesCountAsync()
    {
        var count = _listings.Values.Count(p => p.SellerId == _currentSellerId && p.Status == ListingStatus.Rejected);
        return Task.FromResult(count);
    }

    private void InitializeMockData()
    {
        var seedList = new List<SellerPropertyItemViewModel>
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
                LandSize = 520m,
                Status = ListingStatus.Published,
                PackageCode = "VIP",
                SellerId = _currentSellerId,
                SellerName = _currentSellerName,
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p1.png", "/images/i1.png", "/images/i2.png" },
                Description = "Biệt thự vị trí đắc địa với thiết kế hiện đại, ngập tràn ánh sáng tự nhiên. Pháp lý chuẩn chỉnh, sổ hồng trao tay, công chứng trong ngày.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                Views = 384,
                Inquiries = 29
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
                LandSize = 120m,
                Status = ListingStatus.Published,
                PackageCode = "Boost",
                SellerId = _currentSellerId,
                SellerName = _currentSellerName,
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p3.png", "/images/i1.png" },
                Description = "Tọa lạc trên tuyến phố sầm uất, hạ tầng đồng bộ, an ninh 24/7. Thích hợp vừa ở vừa làm văn phòng công ty hoặc kinh doanh.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-7),
                Views = 245,
                Inquiries = 16
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
                LandSize = 220m,
                Status = ListingStatus.Published,
                PackageCode = "Standard",
                SellerId = _currentSellerId,
                SellerName = _currentSellerName,
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p5.png" },
                Description = "Penthouse thông tầng đẳng cấp quốc tế, ban công kính bao trọn cảnh quan sân golf và công viên Nam Sài Gòn.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-13),
                Views = 188,
                Inquiries = 9
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
                LandSize = 45m,
                Status = ListingStatus.PendingModeration,
                PackageCode = "VIP",
                SellerId = _currentSellerId,
                SellerName = _currentSellerName,
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p1.png" },
                Description = "Căn hộ dịch vụ cao cấp ngay ngã tư Nam Kỳ Khởi Nghĩa và Điện Biên Phủ, đầy đủ nội thất chỉ cần xách vali vào ở.",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
                Views = 45,
                Inquiries = 2
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
                LandSize = 100m,
                Status = ListingStatus.PendingModeration,
                PackageCode = "Standard",
                SellerId = _currentSellerId,
                SellerName = _currentSellerName,
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p2.png" },
                Description = "Nhà phố xây 3 tầng kiên cố, đường 10.5m lề 5m rộng rãi, gần bãi biển Non Nước và bệnh viện Phụ Sản - Nhi.",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-5),
                Views = 32,
                Inquiries = 1
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
                LandSize = 300m,
                Status = ListingStatus.Rejected,
                PackageCode = "Standard",
                SellerId = _currentSellerId,
                SellerName = _currentSellerName,
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p4.png" },
                Description = "Mặt bằng tòa nhà văn phòng tiêu chuẩn quốc tế hạng A, quản lý chuyên nghiệp CBRE.",
                RejectReason = "Hình ảnh đăng tải mờ và sai lệch thông tin diện tích thực tế so với quy hoạch.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-6),
                Views = 76,
                Inquiries = 0
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
                LandSize = 65m,
                Status = ListingStatus.Draft,
                PackageCode = "Standard",
                SellerId = _currentSellerId,
                SellerName = _currentSellerName,
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p5.png" },
                Description = "Nhà dân xây kiên cố 5 tầng còn rất mới, ô tô đỗ cửa ngày đêm, khu phân lô quân đội dân trí cao.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-3),
                Views = 0,
                Inquiries = 0
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
                LandSize = 140m,
                Status = ListingStatus.Hidden,
                PackageCode = "Standard",
                SellerId = _currentSellerId,
                SellerName = _currentSellerName,
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p6.png" },
                Description = "Dự án biểu tượng của CapitaLand tại Thảo Điền với hồ bơi đáy kính lơ lửng trên không trung.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-12),
                Views = 152,
                Inquiries = 8
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
                LandSize = 80m,
                Status = ListingStatus.Expired,
                PackageCode = "Standard",
                SellerId = _currentSellerId,
                SellerName = _currentSellerName,
                SellerAvatar = "/images/avatar-1.png",
                Images = new List<string> { "/images/p1.png" },
                Description = "Khu phố ẩm thực Phan Xích Long sầm uất, hẻm thông bàn cờ, thuận tiện di chuyển sang Quận 1 chỉ 5 phút.",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-35),
                Views = 312,
                Inquiries = 22
            }
        };

        foreach (var s in seedList)
        {
            _listings[s.Id] = s;

            // Seed sample Delta history for each
            _histories[s.Id] = new List<HistoryDeltaViewModel>
            {
                new()
                {
                    HistoryId = 1001,
                    ListingId = s.Id,
                    ActionType = "Tạo tin ban đầu (F02)",
                    Actor = _currentSellerName,
                    ActionDate = s.CreatedAt,
                    Note = "Đăng ký tạo tin bất động sản",
                    DeltaChanges = new List<DeltaItemViewModel>
                    {
                        new() { Field = "Status", OldValue = null, NewValue = s.Status.ToString() },
                        new() { Field = "Price", OldValue = null, NewValue = FormatHelper.FormatPrice(s.Price, s.ListingType) },
                        new() { Field = "PackageCode", OldValue = null, NewValue = s.PackageCode }
                    }
                }
            };
        }

        // Initialize Wallet transactions
        _wallet.Transactions.AddRange(new List<WalletTransactionViewModel>
        {
            new()
            {
                Id = "TX-89104",
                Type = "Charge",
                Amount = -500_000m,
                BalanceAfter = 5_250_000m,
                Description = "Thanh toán gói VIP Nổi bật cho tin đăng PN-1000 (30 ngày)",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                ListingId = "PN-1000",
                CorrelationId = "0a5d2b1f-7b2e-4b2a-8c5e-0f1e2d3c4b5a"
            },
            new()
            {
                Id = "TX-89021",
                Type = "Charge",
                Amount = -200_000m,
                BalanceAfter = 5_750_000m,
                Description = "Thanh toán gói Đẩy tin Boost cho tin đăng PN-1034 (14 ngày)",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-7),
                ListingId = "PN-1034",
                CorrelationId = "9f8e7d6c-5b4a-3f2e-1d0c-b9a8f7e6d5c4"
            },
            new()
            {
                Id = "TX-88750",
                Type = "TopUp",
                Amount = 2_000_000m,
                BalanceAfter = 5_950_000m,
                Description = "Nạp tiền qua Thẻ tín dụng Quốc tế Visa *** 4829",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
                CorrelationId = "1a2b3c4d-5e6f-7a8b-9c0d-e1f2a3b4c5d6"
            },
            new()
            {
                Id = "TX-88210",
                Type = "Refund",
                Amount = 500_000m,
                BalanceAfter = 3_950_000m,
                Description = "Bồi hoàn Saga (Saga Compensation Refund) do giao dịch nâng cấp quá hạn",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-20),
                ListingId = "PN-1153",
                CorrelationId = "4d3c2b1a-0f9e-8d7c-6b5a-4f3e2d1c0b9a"
            }
        });
    }
}
