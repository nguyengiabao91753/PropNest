using Microsoft.AspNetCore.Mvc;
using PropNest.Web.Seller.Filters;
using PropNest.Web.Seller.Helpers;
using PropNest.Web.Seller.Models.InputModels;
using PropNest.Web.Seller.Services.IServices;

namespace PropNest.Web.Seller.Controllers;

[SellerAuthorize]
[Route("Wallet")]
[Route("seller/wallet")]
public class WalletController : Controller
{
    private readonly ISellerWalletService _sellerService;

    public WalletController(ISellerWalletService sellerService)
    {
        _sellerService = sellerService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        [FromQuery] int page = 1,
        [FromQuery] string? type = null,
        [FromQuery] string? q = null,
        CancellationToken cancellationToken = default)
    {
        ViewData["ActiveNav"] = "Wallet";
        var model = await _sellerService.GetWalletAsync(page, type, q, cancellationToken);
        return View(model);
    }

    [HttpPost("TopUp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TopUp([FromForm] TopUpInputModel input, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Số tiền nạp không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var success = await _sellerService.TopUpWalletAsync(input.Amount, null, cancellationToken);
        if (success)
        {
            TempData["SuccessMessage"] = $"Nạp thành công {FormatHelper.FormatVnd(input.Amount)} vào số dư chính!";
        }
        else
        {
            TempData["ErrorMessage"] = "Nạp tiền thất bại. Vui lòng thử lại sau.";
        }

        return RedirectToAction(nameof(Index));
    }
}
