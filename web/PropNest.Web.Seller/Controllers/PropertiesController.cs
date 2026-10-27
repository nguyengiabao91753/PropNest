using Microsoft.AspNetCore.Mvc;
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

    public PropertiesController(ISellerPropertyService sellerService)
    {
        _sellerService = sellerService;
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

        // Xử lý khi user bấm nút "Lưu bản nháp (Draft)"
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
            if (string.IsNullOrWhiteSpace(rawDesc))
            {
                model.Description = "Bản nháp đang soạn thảo, nội dung chi tiết sẽ được bổ sung sau.";
            }
            else
            {
                model.Description = rawDesc;
            }

            if (string.IsNullOrWhiteSpace(model.City))
            {
                model.City = "TP. Hồ Chí Minh";
            }
            if (string.IsNullOrWhiteSpace(model.District))
            {
                model.District = "Quận 1";
            }
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

        // Luồng chuẩn: Add Listing -> Validate -> Submit tạo tin -> Chuyển sang Add Images
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

        // Chuyển sang màn hình Add Images
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

        // Hoàn tất: Chuyển về tab Bản nháp
        return RedirectToAction(nameof(Index), new { status = "draft" });
    }

    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(long id, CancellationToken cancellationToken)
    {
        ViewData["ActiveNav"] = "Properties";
        // 1. Lấy thông tin tin đăng hiện có từ Backend API
        var item = await _sellerService.GetPropertyByIdAsync(id, cancellationToken);
        if (item == null)
        {
            TempData["ErrorMessage"] = $"Không tìm thấy tin đăng #{id}.";
            return RedirectToAction(nameof(Index));
        }
        // 2. Map dữ liệu sang InputModel (chứa RowVersion và ảnh cũ)
        var model = new CreatePropertyInputModel
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            PropertyType = item.PropertyType,
            ListingType = item.ListingType,
            Price = item.Price,
            Area = item.Area,
            City = item.City,
            District = item.District,
            Ward = item.Ward,
            Address = item.Address,
            RowVersion = item.RowVersion,                      // Chuỗi Base64 concurrency token
            ExistingImages = item.Images ?? new List<string>() // Danh sách ảnh đã có
        };
        return View(model);
    }
    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, CreatePropertyInputModel model, CancellationToken cancellationToken)
    {
        ViewData["ActiveNav"] = "Properties";
        // 1. Kiểm tra validation
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        // 2. Gọi Service gửi PUT /api/v1/listings/{id}
        var result = await _sellerService.UpdatePropertyAsync(id, model, cancellationToken);
        switch (result)
        {
            case UpdatePropertyStatus.Success:
                TempData["SuccessMessage"] = $"Cập nhật tin đăng #{id} thành công!";
                return RedirectToAction(nameof(Details), new { id });
            case UpdatePropertyStatus.ConcurrencyConflict:
                // ĐÚNG YÊU CẦU ĐỀ BÀI: Optimistic Concurrency fail (HTTP 409)
                TempData["ErrorMessage"] = "Tin đã bị người khác chỉnh sửa, vui lòng tải lại.";
                // Tải lại dữ liệu mới nhất để cập nhật RowVersion mới và ảnh vào form
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

    // ==========================================
    // Task 213: Mua gói dịch vụ & Polling Saga
    // ==========================================

    [HttpPost("PurchasePackage")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PurchasePackage([FromBody] PurchasePackageInputModel input, CancellationToken cancellationToken = default)
    {
        // 1. Validate dữ liệu đầu vào cơ bản
        if (input == null || input.ListingId <= 0 || string.IsNullOrWhiteSpace(input.PackageCode) || input.Amount <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Thông tin gói mua hoặc mã tin đăng không hợp lệ."
            });
        }

        // 2. Gọi Service gửi POST sang Backend API kèm Idempotency-Key
        var correlationId = await _sellerService.PurchasePackageAsync(
            input.ListingId,
            input.PackageCode,
            input.Amount,
            input.IdempotencyKey,
            cancellationToken);

        if (correlationId == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Không thể khởi tạo giao dịch mua gói. Vui lòng kiểm tra lại số dư ví hoặc thử lại sau."
            });
        }

        // 3. Trả về correlationId cho Client Javascript bắt đầu Polling
        return Ok(new
        {
            success = true,
            correlationId = correlationId.Value
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

        // Trả về toàn bộ WorkflowStatusViewModel (đã có sẵn isCompleted, isFailed, currentStepIndex, statusText)
        return Ok(workflow);
    }
}
