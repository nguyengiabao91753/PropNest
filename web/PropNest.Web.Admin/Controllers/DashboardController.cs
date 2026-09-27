using Microsoft.AspNetCore.Mvc;
using PropNest.Web.Admin.Services;

namespace PropNest.Web.Admin.Controllers;

[Route("")]
[Route("Dashboard")]
public class DashboardController : Controller
{
    private readonly IAdminListingService _listingService;

    public DashboardController(IAdminListingService listingService)
    {
        _listingService = listingService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        ViewData["ActiveNav"] = "Dashboard";
        var model = await _listingService.GetDashboardDataAsync();
        return View(model);
    }
}
