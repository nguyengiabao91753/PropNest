using Microsoft.AspNetCore.Mvc;
using PropNest.Web.Client.Services;

namespace PropNest.Web.Client.Controllers;

public class HomeController : Controller
{
    private readonly IClientListingService _listingService;

    public HomeController(IClientListingService listingService)
    {
        _listingService = listingService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var model = await _listingService.GetHomeDataAsync();
        return View(model);
    }
}
