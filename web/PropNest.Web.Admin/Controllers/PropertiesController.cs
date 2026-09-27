using Microsoft.AspNetCore.Mvc;
using PropNest.Web.Admin.Services;

namespace PropNest.Web.Admin.Controllers;

[Route("Properties")]
[Route("Admin/Properties")]
public class PropertiesController : Controller
{
    private readonly IAdminListingService _listingService;

    public PropertiesController(IAdminListingService listingService)
    {
        _listingService = listingService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index([FromQuery] string? status = "all", [FromQuery] string? q = null)
    {
        ViewData["ActiveNav"] = "Properties";
        var model = await _listingService.GetPropertiesAsync(status, q);
        return View(model);
    }

    [HttpGet("Details/{id}")]
    public async Task<IActionResult> Details(string id)
    {
        ViewData["ActiveNav"] = "Properties";
        var item = await _listingService.GetPropertyByIdAsync(id);
        if (item == null)
        {
            return NotFound();
        }
        return View(item);
    }
}
