using Microsoft.AspNetCore.Mvc;
using PropNest.Web.Client.Helpers;
using PropNest.Web.Client.Models.ViewModels;
using PropNest.Web.Client.Services;
using PropNest.Web.Client.Services.IServices;

namespace PropNest.Web.Client.Controllers;

[Route("")]
public class AuthController : Controller
{
    private readonly IClientAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IClientAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromForm] LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // 1. Gọi Backend API đăng nhập
        var result = await _authService.LoginAsync(model);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Token))
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Đăng nhập không thành công.");
            return View(model);
        }

        // 2. Lưu JWT vào HttpOnly Cookie bảo mật
        Response.Cookies.Append(AuthHeaderHandler.TokenCookieName, result.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = model.RememberMe == true ? DateTimeOffset.UtcNow.AddDays(7) : null
        });

        TempData["SuccessMessage"] = "Đăng nhập thành công! Chào mừng bạn trở lại PropNest.";

        // Quay lại trang xem dở hoặc Trang chủ
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
    public async Task<IActionResult> Signup([FromForm] SignupViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // 1. Gọi Backend API đăng ký tài khoản
        var result = await _authService.RegisterAsync(model);
        if (!result.Succeeded)
        {
            // Map lỗi chi tiết từ API vào ModelState (trùng email, mật khẩu yếu...)
            foreach (var (field, messages) in result.Errors)
            {
                foreach (var msg in messages)
                {
                    ModelState.AddModelError(field, msg);
                }
            }

            if (result.Errors.Count == 0 && !string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage);
            }

            return View(model);
        }

        TempData["SuccessMessage"] = $"Đăng ký thành công tài khoản {model.FullName}! Vui lòng đăng nhập để tiếp tục.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("logout")]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // Xóa Cookie JWT
        Response.Cookies.Delete(AuthHeaderHandler.TokenCookieName);
        TempData["SuccessMessage"] = "Bạn đã đăng xuất thành công.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var token = Request.Cookies[AuthHeaderHandler.TokenCookieName];
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { message = "Chưa đăng nhập." });
        }

        // Ưu tiên lấy từ Backend API
        var profile = await _authService.GetUserProfileAsync();
        // Fallback đọc trực tiếp từ JWT
        profile ??= JwtUserHelper.GetProfileFromToken(token);

        if (profile == null)
        {
            return NotFound(new { message = "Không tìm thấy hồ sơ người dùng." });
        }

        return Json(profile);
    }
}
