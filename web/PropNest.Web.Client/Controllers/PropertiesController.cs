using Microsoft.AspNetCore.Mvc;
using PropNest.Web.Client.Models.InputModels;
using PropNest.Web.Client.Services;

namespace PropNest.Web.Client.Controllers;

[Route("properties")]
public class PropertiesController : Controller
{
    private readonly IClientListingService _listingService;

    public PropertiesController(IClientListingService listingService)
    {
        _listingService = listingService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Details(string id)
    {
        var model = await _listingService.GetPropertyDetailAsync(id);
        if (model == null)
        {
            ViewData["NotFoundId"] = id;
            return View("NotFound");
        }

        return View(model);
    }

    [HttpPost("inquiry")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inquiry([FromForm] InquiryInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ họ tên, thông tin liên hệ và lời nhắn.";
            return RedirectToAction(nameof(Details), new { id = input.ListingId });
        }

        await _listingService.SendInquiryAsync(input);
        TempData["SuccessMessage"] = "Đã gửi yêu cầu liên hệ thành công! Người bán sẽ nhận được tin nhắn và phản hồi sớm nhất.";

        return RedirectToAction(nameof(Details), new { id = input.ListingId });
    }
}
