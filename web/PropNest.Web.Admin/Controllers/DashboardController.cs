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

    [HttpGet("/api/v1/admin/dashboard-stats")]
    [HttpGet("Stats")]
    public async Task<IActionResult> GetDashboardStatsApi()
    {
        var model = await _listingService.GetDashboardDataAsync();
        return Json(new
        {
            publishedCount = model.PublishedCount,
            pendingCount = model.PendingCount,
            totalUsers = model.TotalUsers,
            totalWalletTransactions = model.TotalWalletTransactions,
            totalWalletVolume = model.TotalWalletVolume,
            totalProperties = model.TotalProperties,
            monthlyMetrics = model.MonthlyMetrics,
            statusDistribution = model.StatusDistribution
        });
    }
}
