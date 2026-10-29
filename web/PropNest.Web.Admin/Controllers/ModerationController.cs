using Microsoft.AspNetCore.Mvc;
using PropNest.Web.Admin.Filters;
using PropNest.Web.Admin.Models.InputModels;
using PropNest.Web.Admin.Services.IServices;

namespace PropNest.Web.Admin.Controllers;

[AdminAuthorize]
[Route("Moderation")]
[Route("Admin/Moderation")]
[Route("Admin/Approve")]
[Route("Admin/Queue")]
public class ModerationController : Controller
{
    private readonly IAdminListingService _listingService;

    public ModerationController(IAdminListingService listingService)
    {
        _listingService = listingService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index([FromQuery] string? id = null)
    {
        ViewData["ActiveNav"] = "Moderation";
        var model = await _listingService.GetModerationQueueAsync(id);
        return View(model);
    }

    [HttpGet("Detail/{id}")]
    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(string id)
    {
        ViewData["ActiveNav"] = "Moderation";
        var item = await _listingService.GetPropertyByIdAsync(id);
        if (item is null)
        {
            TempData["ErrorMessage"] = $"Không tìm thấy tin đăng {id} để kiểm duyệt.";
            return RedirectToAction(nameof(Index));
        }

        return View(item);
    }

    [HttpGet("/api/v1/moderation/listings")]
    public async Task<IActionResult> GetPendingListingsApi()
    {
        var model = await _listingService.GetModerationQueueAsync();
        return Json(new { total = model.TotalPending, items = model.PendingProperties });
    }

    [HttpGet("/api/v1/moderation/listings/{id}")]
    public async Task<IActionResult> GetListingDetailApi(string id)
    {
        var item = await _listingService.GetPropertyByIdAsync(id);
        return item is null
            ? NotFound(new { success = false, message = $"Không tìm thấy tin đăng {id}." })
            : Json(item);
    }

    [HttpPost("/api/v1/moderation/listings/{id}/approve")]
    [HttpPost("Approve/{id}")]
    public async Task<IActionResult> Approve(string id)
    {
        var success = await _listingService.ApproveListingAsync(id);

        if (Request.Path.Value?.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) == true
            || Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            if (success)
            {
                return Ok(new { success = true, id, message = "Tin đăng đã được duyệt thành công!" });
            }
            return NotFound(new { success = false, message = $"Không tìm thấy tin đăng {id} để phê duyệt." });
        }

        if (success)
        {
            TempData["SuccessMessage"] = "Tin đăng đã được duyệt thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = $"Không tìm thấy tin đăng {id} để phê duyệt.";
        }

        return RedirectToAction(nameof(Index));
    }

    public sealed record RejectApiPayload(string? Reason, IReadOnlyList<string>? Reasons, string? ReasonsJson);

    [HttpPost("/api/v1/moderation/listings/{id}/reject")]
    public async Task<IActionResult> RejectApi(string id, [FromBody] RejectApiPayload? payload)
    {
        var reason = payload?.Reason
            ?? (payload?.Reasons is { Count: > 0 } ? string.Join("; ", payload.Reasons) : null)
            ?? payload?.ReasonsJson
            ?? "Không đạt tiêu chuẩn kiểm duyệt";

        var success = await _listingService.RejectListingAsync(id, reason);
        if (success)
        {
            return Ok(new { success = true, id, message = $"Đã từ chối duyệt tin {id} thành công!" });
        }

        return NotFound(new { success = false, message = $"Không tìm thấy tin đăng {id} để từ chối." });
    }

    [HttpPost("Reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject([FromForm] RejectPropertyInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do từ chối cụ thể theo yêu cầu F03.";
            return RedirectToAction(nameof(Index), new { id = input.ListingId });
        }

        var success = await _listingService.RejectListingAsync(input.ListingId, input.Reason);
        if (success)
        {
            TempData["WarningMessage"] = $"Đã từ chối duyệt tin {input.ListingId} thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = $"Không tìm thấy tin đăng {input.ListingId} để từ chối.";
        }

        return RedirectToAction(nameof(Index));
    }
}
