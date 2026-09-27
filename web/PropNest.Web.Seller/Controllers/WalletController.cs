using Microsoft.AspNetCore.Mvc;
using PropNest.Web.Seller.Helpers;
using PropNest.Web.Seller.Models.InputModels;
using PropNest.Web.Seller.Services;

namespace PropNest.Web.Seller.Controllers;

[Route("Wallet")]
[Route("seller/wallet")]
public class WalletController : Controller
{
    private readonly ISellerPropertyService _sellerService;

    public WalletController(ISellerPropertyService sellerService)
    {
        _sellerService = sellerService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        ViewData["ActiveNav"] = "Wallet";
        var model = await _sellerService.GetWalletAsync();
        return View(model);
    }

    [HttpPost("TopUp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TopUp([FromForm] TopUpInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Số tiền nạp không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var success = await _sellerService.TopUpWalletAsync(input.Amount);
        if (success)
        {
            TempData["SuccessMessage"] = $"Nạp thành công {FormatHelper.FormatVnd(input.Amount)} vào số dư chính!";
        }

        return RedirectToAction(nameof(Index));
    }
}
