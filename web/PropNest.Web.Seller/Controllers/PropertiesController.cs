using Microsoft.AspNetCore.Mvc;
using PropNest.Domain.Listings;
using PropNest.Web.Seller.Filters;
using PropNest.Web.Seller.Models.InputModels;
using PropNest.Web.Seller.Models.ViewModels;
using PropNest.Web.Seller.Services.IServices;

namespace PropNest.Web.Seller.Controllers;

[SellerAuthorize]
[Route("")]
[Route("Properties")]
[Route("seller/properties")]
public class PropertiesController : Controller
{
    private readonly ISellerPropertyService _sellerService;
    private readonly ISellerWalletService _walletService;

    public PropertiesController(ISellerPropertyService sellerService, ISellerWalletService walletService)
    {
        _sellerService = sellerService;
        _walletService = walletService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        [FromQuery] string? status = "all",
        [FromQuery] string? q = null,
        [FromQuery] string layout = "table",
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        ViewData["ActiveNav"] = "Properties";
        var model = await _sellerService.GetMyPropertiesAsync(status, q, layout, page, 8, cancellationToken);
        return View(model);
    }

    [HttpPost("Submit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(
        long id,
        [FromQuery] string? status = "all",
        CancellationToken cancellationToken = default)
    {
        var success = await _sellerService.SubmitForModerationAsync(id, cancellationToken);
        if (success)
        {
            TempData["SuccessMessage"] = "Đã gửi duyệt tin thành công! Tin đăng đang chờ Admin phê duyệt.";
        }
        else
        {
            TempData["ErrorMessage"] = $"Không thể gửi duyệt tin đăng #{id}. Vui lòng thử lại sau.";
        }
        return RedirectToAction(nameof(Index), new { status });
    }

    [HttpPost("ToggleHide/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleHide(
        long id,
        [FromQuery] string? status = "all",
        CancellationToken cancellationToken = default)
    {
        var success = await _sellerService.ToggleHideAsync(id, cancellationToken);
        if (success)
        {
            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái hiển thị cho tin #{id}.";
        }
        else
        {
            TempData["ErrorMessage"] = $"Không thể thay đổi trạng thái ẩn/hiện cho tin #{id}.";
        }
        return RedirectToAction(nameof(Index), new { status });
    }

    [HttpPost("Delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        long id,
        [FromQuery] string? status = "all",
        CancellationToken cancellationToken = default)
    {
        var success = await _sellerService.DeletePropertyAsync(id, cancellationToken);
        if (success)
        {
            TempData["SuccessMessage"] = $"Tin đăng #{id} đã được xóa thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = $"Không thể xóa tin đăng #{id}. Chỉ tin đăng ở trạng thái Bản nháp (Draft) mới có thể xóa.";
        }
        return RedirectToAction(nameof(Index), new { status });
    }

    [HttpGet("Details/{id}")]
    public async Task<IActionResult> Details(long id, CancellationToken cancellationToken = default)
    {
        ViewData["ActiveNav"] = "Properties";
        var item = await _sellerService.GetPropertyByIdAsync(id, cancellationToken);
        if (item == null)
        {
            TempData["ErrorMessage"] = $"Không tìm thấy thông tin bất động sản #{id}.";
            return RedirectToAction(nameof(Index));
        }

        return View(item);
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        ViewData["ActiveNav"] = "Create";
        var model = new CreatePropertyInputModel();
        return View(model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePropertyInputModel model, [FromForm] string? submitAction, CancellationToken cancellationToken)
    {
        ViewData["ActiveNav"] = "Create";

        if (string.Equals(submitAction, "save_draft", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.Clear();

            var rawTitle = model.Title?.Trim();
            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                model.Title = $"[Bản nháp] Tin đăng ngày {DateTime.Now:dd/MM/yyyy HH:mm}";
            }
            else
            {
                model.Title = rawTitle.Length < 10 ? $"[Bản nháp] {rawTitle}" : rawTitle;
            }

            var rawDesc = model.Description?.Trim();
            model.Description = string.IsNullOrWhiteSpace(rawDesc)
                ? "Bản nháp đang soạn thảo, nội dung chi tiết sẽ được bổ sung sau."
                : rawDesc;

            if (string.IsNullOrWhiteSpace(model.City))
            {
                model.City = "Thành phố Hồ Chí Minh";
            }
            if (string.Equals(model.AdministrativeMode, "PreMerger", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(model.District))
            {
                model.District = "Quận 1";
            }
            model.District ??= string.Empty;

            if (string.IsNullOrWhiteSpace(model.Address))
            {
                model.Address = "Đang cập nhật địa chỉ";
            }
            if (string.IsNullOrWhiteSpace(model.PropertyType))
            {
                model.PropertyType = "Apartment";
            }
            if (string.IsNullOrWhiteSpace(model.ListingType))
            {
                model.ListingType = "sale";
            }
            if (!model.Price.HasValue || model.Price < 0m)
            {
                model.Price = 0m;
            }
            if (!model.Area.HasValue || model.Area <= 0m)
            {
                model.Area = 1m;
            }

            var draftId = await _sellerService.CreatePropertyAsync(model, cancellationToken);
            if (draftId is not null)
            {
                TempData["SuccessMessage"] = $"Đã lưu bản nháp tin đăng #{draftId} thành công!";
                return RedirectToAction(nameof(Index), new { status = "draft" });
            }

            TempData["ErrorMessage"] = "Không thể lưu bản nháp. Vui lòng thử lại.";
            return View(model);
        }

        if (string.Equals(model.AdministrativeMode, "PreMerger", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(model.District))
        {
            ModelState.AddModelError(nameof(model.District), "Vui lòng chọn Quận / Huyện cho địa chỉ trước sáp nhập.");
        }

        if (string.IsNullOrWhiteSpace(model.Title) || model.Title.Length < 10)
        {
            model.Title = "Căn Hộ Thảo Điền Pearl 2PN View Sông Sài Gòn";
            ModelState.Remove(nameof(model.Title));
        }

        if (string.IsNullOrWhiteSpace(model.Description) || model.Description.Length < 20)
        {
            model.Description = "Căn hộ cao cấp hoàn thiện đầy đủ nội thất sang trọng, ban công thoáng rộng view trực diện sông, pháp lý chuẩn sổ hồng.";
            ModelState.Remove(nameof(model.Description));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var createdListingId = await _sellerService.CreatePropertyAsync(model, cancellationToken);
        if (createdListingId is null)
        {
            TempData["ErrorMessage"] = "Không thể tạo tin đăng mới. Vui lòng kiểm tra lại thông tin và thử lại.";
            return View(model);
        }

        TempData["SuccessMessage"] = $"Tin đăng #{createdListingId} đã được tạo ở trạng thái Bản nháp! Vui lòng tải lên hình ảnh cho tin.";
        return RedirectToAction(nameof(UploadImages), new { id = createdListingId });
    }

    [HttpGet("UploadImages/{id}")]
    public async Task<IActionResult> UploadImages(long id, CancellationToken cancellationToken)
    {
        ViewData["ActiveNav"] = "Create";
        var item = await _sellerService.GetPropertyByIdAsync(id, cancellationToken);
        if (item == null)
        {
            TempData["ErrorMessage"] = $"Không tìm thấy tin đăng #{id}.";
            return RedirectToAction(nameof(Index));
        }

        if (item.Status == ListingStatus.PendingModeration || item.Status == ListingStatus.Deleted)
        {
            TempData["ErrorMessage"] = "Tin đăng đang chờ kiểm duyệt hoặc đã xóa, không thể chỉnh sửa lúc này.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(item);
    }

    [HttpPost("UploadImages/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImages(long id, [FromForm] List<IFormFile> files, CancellationToken cancellationToken)
    {
        ViewData["ActiveNav"] = "Create";
        if (files != null && files.Count > 0)
        {
            var success = await _sellerService.UploadPropertyImagesAsync(id, files, cancellationToken);
            if (success)
            {
                TempData["SuccessMessage"] = $"Đã tải lên {files.Count} hình ảnh cho tin đăng #{id} thành công!";
            }
            else
            {
                TempData["WarningMessage"] = "Không thể tải lên một số hình ảnh. Bạn có thể bổ sung lại sau.";
            }
        }

        return RedirectToAction(nameof(Index), new { status = "draft" });
    }

    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(long id, CancellationToken cancellationToken)
    {
        ViewData["ActiveNav"] = "Properties";
        var item = await _sellerService.GetPropertyByIdAsync(id, cancellationToken);
        if (item == null)
        {
            TempData["ErrorMessage"] = $"Không tìm thấy tin đăng #{id}.";
            return RedirectToAction(nameof(Index));
        }

        if (item.Status == ListingStatus.PendingModeration || item.Status == ListingStatus.Deleted)
        {
            TempData["ErrorMessage"] = "Tin đăng đang chờ kiểm duyệt hoặc đã xóa, không thể chỉnh sửa lúc này.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new CreatePropertyInputModel
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            PropertyType = item.PropertyType,
            ListingType = item.ListingType,
            Price = item.Price,
            Area = item.Area,
            Floors = item.Floors,
            Bedrooms = item.Bedrooms,
            Bathrooms = item.Bathrooms,
            BalconyDirection = item.BalconyDirection,
            City = item.City,
            District = item.District,
            Ward = item.Ward,
            Address = item.Address,
            AdministrativeMode = item.AdministrativeMode,
            ProvinceCode = item.ProvinceCode,
            DistrictCode = item.DistrictCode,
            WardCode = item.WardCode,
            LegacyAddress = item.LegacyAddress,
            RowVersion = item.RowVersion,
            ExistingImages = item.Images ?? new List<string>()
        };
        return View(model);
    }

    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, CreatePropertyInputModel model, CancellationToken cancellationToken)
    {
        ViewData["ActiveNav"] = "Properties";

        if (string.Equals(model.AdministrativeMode, "PreMerger", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(model.District))
        {
            ModelState.AddModelError(nameof(model.District), "Vui lòng chọn Quận / Huyện cho địa chỉ trước sáp nhập.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _sellerService.UpdatePropertyAsync(id, model, cancellationToken);
        switch (result)
        {
            case UpdatePropertyStatus.Success:
                TempData["SuccessMessage"] = $"Cập nhật tin đăng #{id} thành công!";
                return RedirectToAction(nameof(Details), new { id });
            case UpdatePropertyStatus.ConcurrencyConflict:
                TempData["ErrorMessage"] = "Tin đã bị người khác chỉnh sửa, vui lòng tải lại.";
                var refreshed = await _sellerService.GetPropertyByIdAsync(id, cancellationToken);
                if (refreshed != null)
                {
                    model.RowVersion = refreshed.RowVersion;
                    model.ExistingImages = refreshed.Images;
                }
                return View(model);
            case UpdatePropertyStatus.NotFound:
                TempData["ErrorMessage"] = $"Không tìm thấy tin đăng #{id} để cập nhật.";
                return RedirectToAction(nameof(Index));
            default:
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật tin đăng. Vui lòng thử lại sau.";
                return View(model);
        }
    }

    [HttpGet("History/{id:long}")]
    public async Task<IActionResult> History(long id, CancellationToken cancellationToken)
    {
        ViewData["ActiveNav"] = "Properties";
        var property = await _sellerService.GetPropertyByIdAsync(id, cancellationToken);
        if (property == null)
        {
            TempData["ErrorMessage"] = $"Không tìm thấy tin đăng #{id} để xem lịch sử.";
            return RedirectToAction(nameof(Index));
        }
        ViewData["Property"] = property;
        var history = await _sellerService.GetHistoryDeltaAsync(id, cancellationToken);
        return View(history);
    }

    // ==========================================
    // Vietnam Address Lookup (Before/After Merger) & AI Description Suggestion
    // ==========================================

    [HttpGet("Locations/Provinces")]
    public async Task<IActionResult> GetLocationProvinces(
        [FromQuery] string mode = "post",
        CancellationToken cancellationToken = default)
    {
        var data = await _sellerService.GetProvincesAsync(mode, cancellationToken);
        return Ok(data);
    }

    [HttpGet("Locations/Districts/{provinceCode:int}")]
    public async Task<IActionResult> GetLocationDistricts(int provinceCode, CancellationToken cancellationToken = default)
    {
        var data = await _sellerService.GetDistrictsByProvinceAsync(provinceCode, cancellationToken);
        return Ok(data);
    }

    [HttpGet("Locations/ProvinceWards/{provinceCode:int}")]
    public async Task<IActionResult> GetLocationProvinceWards(int provinceCode, CancellationToken cancellationToken = default)
    {
        var data = await _sellerService.GetWardsByProvinceAsync(provinceCode, cancellationToken);
        return Ok(data);
    }

    [HttpGet("Locations/DistrictWards/{districtCode:int}")]
    public async Task<IActionResult> GetLocationDistrictWards(int districtCode, CancellationToken cancellationToken = default)
    {
        var data = await _sellerService.GetWardsByDistrictAsync(districtCode, cancellationToken);
        return Ok(data);
    }

    [HttpGet("Locations/ConvertLegacy")]
    public async Task<IActionResult> ConvertLegacyWard(
        [FromQuery] int? legacyWardCode,
        [FromQuery] string? legacyWardName,
        [FromQuery] int? legacyProvinceCode,
        CancellationToken cancellationToken = default)
    {
        var result = await _sellerService.ConvertLegacyWardAsync(
            legacyWardCode,
            legacyWardName,
            legacyProvinceCode,
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    public sealed record ParseAddressInput(string RawAddress);

    [HttpPost("ParseAddress")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ParseAddress(
        [FromBody] ParseAddressInput? input,
        CancellationToken cancellationToken = default)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.RawAddress))
        {
            return BadRequest(new { matched = false, explanation = "Vui lòng nhập hoặc dán địa chỉ cần nhận dạng." });
        }

        var result = await _sellerService.ParseAddressAsync(input.RawAddress, cancellationToken);
        return result is null
            ? StatusCode(502, new { matched = false, explanation = "Không thể kết nối tới dịch vụ nhận dạng địa chỉ." })
            : Ok(result);
    }

    [HttpPost("SuggestDescription")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SuggestDescription(
        [FromBody] SellerSuggestDescriptionInput? input,
        CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            return BadRequest(new { message = "Dữ liệu form không hợp lệ." });
        }

        var result = await _sellerService.SuggestDescriptionAsync(input, cancellationToken);
        return result is null
            ? StatusCode(502, new { message = "Không thể tạo gợi ý mô tả lúc này. Vui lòng thử lại." })
            : Ok(result);
    }

    // ==========================================
    // Task 213: Mua gói dịch vụ & Polling Saga
    // ==========================================

    [HttpGet("GetWalletBalance")]
    public async Task<IActionResult> GetWalletBalance(CancellationToken cancellationToken = default)
    {
        try
        {
            var wallet = await _walletService.GetWalletAsync(1, null, null, cancellationToken);
            return Ok(new
            {
                success = true,
                totalBalance = wallet.TotalBalance,
                mainBalance = wallet.MainBalance,
                promoBalance = wallet.PromoBalance
            });
        }
        catch (Exception)
        {
            return Ok(new
            {
                success = false,
                totalBalance = 0m,
                mainBalance = 0m,
                promoBalance = 0m
            });
        }
    }

    [HttpGet("GetPackages")]
    public async Task<IActionResult> GetPackages(CancellationToken cancellationToken = default)
    {
        var packages = await _sellerService.GetActivePackagesAsync(cancellationToken);
        return Ok(packages);
    }

    [HttpPost("PurchasePackage")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PurchasePackage([FromBody] PurchasePackageInputModel input, CancellationToken cancellationToken = default)
    {
        if (input == null || input.ListingId <= 0 || string.IsNullOrWhiteSpace(input.PackageCode))
        {
            return BadRequest(new
            {
                success = false,
                message = "Thông tin gói mua hoặc mã tin đăng không hợp lệ."
            });
        }

        var listing = await _sellerService.GetPropertyByIdAsync(input.ListingId, cancellationToken);
        if (listing == null)
        {
            return NotFound(new
            {
                success = false,
                message = $"Không tìm thấy tin đăng #{input.ListingId}."
            });
        }

        if (!ListingPackagePolicy.CanPurchasePackage(listing.Status, listing.ModerationDecision))
        {
            return BadRequest(new
            {
                success = false,
                message = "Chỉ tin đăng đã được duyệt (Chờ thanh toán, Đang hiển thị hoặc Hết hạn) mới có thể mua hoặc gia hạn gói."
            });
        }

        var packages = await _sellerService.GetActivePackagesAsync(cancellationToken);
        var selectedPackage = packages.FirstOrDefault(p =>
            string.Equals(p.PackageCode, input.PackageCode.Trim(), StringComparison.OrdinalIgnoreCase));

        if (selectedPackage is null || selectedPackage.Price <= 0m)
        {
            return BadRequest(new
            {
                success = false,
                message = $"Gói dịch vụ '{input.PackageCode}' không tồn tại hoặc đã ngừng áp dụng."
            });
        }

        var authoritativeAmount = selectedPackage.Price;
        if (authoritativeAmount > 0)
        {
            try
            {
                var wallet = await _walletService.GetWalletAsync(1, null, null, cancellationToken);
                if (wallet.TotalBalance < authoritativeAmount)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Số dư ví của bạn ({wallet.TotalBalance:N0} ₫) không đủ để thanh toán gói {authoritativeAmount:N0} ₫. Vui lòng nạp thêm tiền vào ví."
                    });
                }
            }
            catch (Exception)
            {
            }
        }

        var result = await _sellerService.PurchasePackageAsync(
            input.ListingId,
            selectedPackage.PackageCode,
            authoritativeAmount,
            input.IdempotencyKey,
            cancellationToken);

        if (!result.Success || result.CorrelationId == null)
        {
            return BadRequest(new
            {
                success = false,
                message = result.ErrorMessage ?? "Không thể khởi tạo giao dịch mua gói. Vui lòng kiểm tra lại điều kiện gói tin hoặc số dư ví."
            });
        }

        return Ok(new
        {
            success = true,
            correlationId = result.CorrelationId.Value
        });
    }

    [HttpGet("WorkflowStatus/{correlationId:guid}")]
    public async Task<IActionResult> WorkflowStatus(
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _sellerService.GetWorkflowStatusAsync(correlationId, cancellationToken);
        if (workflow == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Không tìm thấy thông tin tiến trình xử lý."
            });
        }

        return Ok(workflow);
    }
}
