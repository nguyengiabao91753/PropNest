using Microsoft.AspNetCore.Mvc;
using PropNest.Web.Client.Models.ViewModels;

namespace PropNest.Web.Client.Controllers;

[Route("")]
public class AuthController : Controller
{
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public IActionResult Login([FromForm] LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim().ToLowerInvariant();

        // Tự động nhận diện vai trò dựa trên tài khoản người dùng theo nghiệp vụ hệ thống
        if (email.Contains("admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["SuccessMessage"] = "Đăng nhập thành công với vai trò Quản trị viên (Admin)!";
            return Redirect("https://localhost:7215/");
        }
        
        if (email.Contains("seller", StringComparison.OrdinalIgnoreCase) || 
            email.Contains("minh.nguyen", StringComparison.OrdinalIgnoreCase))
        {
            TempData["SuccessMessage"] = "Đăng nhập thành công vào Cổng Người bán (Seller Portal)!";
            return Redirect("https://localhost:7006/");
        }

        // Mặc định là tài khoản khách hàng thông thường
        TempData["SuccessMessage"] = $"Đăng nhập thành công! Chào mừng bạn trở lại PropNest.";

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet("signup")]
    public IActionResult Signup([FromQuery] string role = "customer")
    {
        return View(new SignupViewModel { Role = role });
    }

    [HttpPost("signup")]
    [ValidateAntiForgeryToken]
    public IActionResult Signup([FromForm] SignupViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        TempData["SuccessMessage"] = $"Chào mừng {model.Name}! Tài khoản của bạn đã được khởi tạo thành công.";

        if (string.Equals(model.Role, "seller", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect("https://localhost:7006/");
        }

        return RedirectToAction("Index", "Home");
    }
}
