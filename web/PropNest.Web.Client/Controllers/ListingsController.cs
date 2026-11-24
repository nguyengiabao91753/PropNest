using Microsoft.AspNetCore.Mvc;
using PropNest.Web.Client.Models.ViewModels;
using PropNest.Web.Client.Services.IServices;

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
        [FromQuery] string? propertyType = null,
        [FromQuery] string? city = null,
        [FromQuery] string? district = null,
        [FromQuery] string? ward = null,
        [FromQuery] int? provinceCode = null,
        [FromQuery] int? districtCode = null,
        [FromQuery] int? wardCode = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] decimal? minArea = null,
        [FromQuery] decimal? maxArea = null,
        [FromQuery] int? minBedrooms = null,
        [FromQuery] int? minBathrooms = null,
        [FromQuery] string? balconyDirection = null,
        [FromQuery] string sortBy = "default",
        [FromQuery] int page = 1,
        [FromQuery] string layout = "grid",
        CancellationToken cancellationToken = default)
    {
        // 1. Đóng gói các tiêu chí lọc nhận từ URL Query String
        var criteria = new ListingsViewModel
        {
            Query = q,
            Type = type,
            PropertyType = propertyType,
            City = city,
            District = district,
            Ward = ward,
            ProvinceCode = provinceCode,
            DistrictCode = districtCode,
            WardCode = wardCode,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            MinArea = minArea,
            MaxArea = maxArea,
            MinBedrooms = minBedrooms,
            MinBathrooms = minBathrooms,
            BalconyDirection = balconyDirection,
            SortBy = sortBy,
            PageNumber = page < 1 ? 1 : page,
            PageSize = 12, // Mỗi trang hiển thị 12 tin (chia hết cho grid 2, 3, 4 cột)
            Layout = layout
        };
        // 2. Gọi Service để lấy dữ liệu từ Backend API
        var model = await _listingService.SearchListingsAsync(criteria, cancellationToken);
        // 3. Truyền ViewModel sang View để render giao diện
        return View(model);
    }
}
