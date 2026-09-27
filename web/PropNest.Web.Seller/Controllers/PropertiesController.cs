using Microsoft.AspNetCore.Mvc;
using PropNest.Web.Seller.Models.InputModels;
using PropNest.Web.Seller.Services;

namespace PropNest.Web.Seller.Controllers;

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
    public async Task<IActionResult> Index([FromQuery] string? status = "all", [FromQuery] string? q = null, [FromQuery] string layout = "table")
    {
        ViewData["ActiveNav"] = "Properties";
        var model = await _sellerService.GetMyPropertiesAsync(status, q, layout);
        return View(model);
    }

    [HttpGet("New")]
    [HttpGet("Create")]
    [Route("seller/properties/new")]
    public IActionResult Create()
    {
        ViewData["ActiveNav"] = "Create";
        return View(new CreatePropertyInputModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] CreatePropertyInputModel input)
    {
        if (!ModelState.IsValid)
        {
            ViewData["ActiveNav"] = "Create";
            return View(input);
        }

        var newId = await _sellerService.CreatePropertyAsync(input);

        if (input.IsDraft)
        {
            TempData["SuccessMessage"] = $"Tin đăng {newId} đã được lưu vào danh sách Bản nháp (Draft).";
        }
        else
        {
            TempData["SuccessMessage"] = $"Tin đăng {newId} đã được gửi thành công vào hàng đợi kiểm duyệt (F03)!";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(string id)
    {
        ViewData["ActiveNav"] = "Properties";
        var item = await _sellerService.GetPropertyByIdAsync(id);
        if (item == null) return NotFound();

        var model = new CreatePropertyInputModel
        {
            Title = item.Title,
            Description = item.Description,
            PropertyType = item.PropertyType,
            ListingType = item.ListingType,
            Price = item.Price,
            Area = item.Area,
            Beds = item.Beds,
            Baths = item.Baths,
            Carports = item.Carports,
            City = item.City,
            District = item.District,
            Ward = item.Ward,
            Address = item.Address,
            PackageCode = item.PackageCode,
            ImageUrl = item.PrimaryImage
        };

        ViewData["ListingId"] = id;
        return View(model);
    }

    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [FromForm] CreatePropertyInputModel input)
    {
        if (!ModelState.IsValid)
        {
            ViewData["ActiveNav"] = "Properties";
            ViewData["ListingId"] = id;
            return View(input);
        }

        var success = await _sellerService.UpdatePropertyAsync(id, input);
        if (success)
        {
            TempData["SuccessMessage"] = $"Cập nhật tin đăng {id} thành công! Lịch sử thay đổi đã được ghi nhận vào Delta Engine (F04).";
        }
        else
        {
            TempData["ErrorMessage"] = $"Không tìm thấy tin đăng {id}.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Details/{id}")]
    public async Task<IActionResult> Details(string id)
    {
        ViewData["ActiveNav"] = "Properties";
        var item = await _sellerService.GetPropertyByIdAsync(id);
        if (item == null) return NotFound();

        var histories = await _sellerService.GetHistoryDeltaAsync(id);
        ViewData["Histories"] = histories;

        return View(item);
    }

    [HttpPost("Submit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(string id)
    {
        var success = await _sellerService.SubmitForModerationAsync(id);
        if (success)
        {
            TempData["SuccessMessage"] = $"Tin đăng {id} đã được gửi vào hàng đợi duyệt thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = $"Không thể gửi duyệt tin đăng {id}.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ToggleHide/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleHide(string id)
    {
        var success = await _sellerService.ToggleHideAsync(id);
        if (success)
        {
            TempData["WarningMessage"] = $"Đã cập nhật trạng thái hiển thị cho tin {id}.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var success = await _sellerService.DeletePropertyAsync(id);
        if (success)
        {
            TempData["SuccessMessage"] = $"Tin đăng {id} đã được xóa thành công.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("History/{id}")]
    public async Task<IActionResult> History(string id)
    {
        ViewData["ActiveNav"] = "Properties";
        var item = await _sellerService.GetPropertyByIdAsync(id);
        if (item == null) return NotFound();

        var histories = await _sellerService.GetHistoryDeltaAsync(id);
        ViewData["Property"] = item;

        return View(histories);
    }
}
