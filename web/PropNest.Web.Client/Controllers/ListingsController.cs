using Microsoft.AspNetCore.Mvc;
using PropNest.Web.Client.Models.ViewModels;
using PropNest.Web.Client.Services;

namespace PropNest.Web.Client.Controllers;

[Route("listings")]
public class ListingsController : Controller
{
    private readonly IClientListingService _listingService;

    public ListingsController(IClientListingService listingService)
    {
        _listingService = listingService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] string? q = null,
        [FromQuery] string type = "all",
        [FromQuery] string? homeType = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] int beds = 0,
        [FromQuery] int baths = 0,
        [FromQuery] bool savedOnly = false,
        [FromQuery] string sort = "newest",
        [FromQuery] string layout = "grid")
    {
        var criteria = new ListingsViewModel
        {
            Query = q,
            Type = type,
            HomeType = homeType,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            Beds = beds,
            Baths = baths,
            SavedOnly = savedOnly,
            Sort = sort,
            Layout = layout
        };

        var model = await _listingService.SearchListingsAsync(criteria);
        return View(model);
    }
}
