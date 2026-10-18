using Microsoft.AspNetCore.Mvc;
using PropNest.Web.Seller.Filters;
using PropNest.Web.Seller.Models.InputModels;
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
        string id,
        [FromQuery] string? status = "all",
        CancellationToken cancellationToken = default)
    {
        var success = await _sellerService.SubmitForModerationAsync(id, cancellationToken);
        if (success)
        {
            TempData["SuccessMessage"] = $"Tin đăng #{id} đã được gửi vào hàng đợi duyệt thành công!";
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
        string id,
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
        string id,
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
    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken = default)
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
}
