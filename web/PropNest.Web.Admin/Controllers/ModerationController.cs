using Microsoft.AspNetCore.Mvc;
using PropNest.Web.Admin.Models.InputModels;
using PropNest.Web.Admin.Services;

namespace PropNest.Web.Admin.Controllers;

[Route("Moderation")]
[Route("Admin/Approve")]
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

    [HttpPost("Approve/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string id)
    {
        var success = await _listingService.ApproveListingAsync(id);
        if (success)
        {
            TempData["SuccessMessage"] = $"Tin đăng {id} đã được phê duyệt và xuất bản thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = $"Không tìm thấy tin đăng {id} để phê duyệt.";
        }

        return RedirectToAction(nameof(Index));
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
            TempData["WarningMessage"] = $"Tin đăng {input.ListingId} đã bị từ chối với lý do: \"{input.Reason}\"";
        }
        else
        {
            TempData["ErrorMessage"] = $"Không tìm thấy tin đăng {input.ListingId} để từ chối.";
        }

        return RedirectToAction(nameof(Index));
    }
}
