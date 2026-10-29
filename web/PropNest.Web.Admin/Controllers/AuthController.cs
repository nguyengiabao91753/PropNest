using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PropNest.Web.Admin.Helpers;
using PropNest.Web.Admin.Models.InputModels;
using PropNest.Web.Admin.Services.Handler;

namespace PropNest.Web.Admin.Controllers;

[Route("admin")]
public class AuthController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AuthController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        // If already logged in as Admin, redirect to returnUrl or moderation queue
        if (HttpContext.Request.Cookies.TryGetValue(AuthHeaderHandler.AdminTokenCookieName, out var token) &&
            !string.IsNullOrWhiteSpace(token))
        {
            var profile = JwtAdminHelper.GetProfileFromToken(token);
            if (profile != null && profile.IsAdmin)
            {
                return LocalRedirect(GetSafeReturnUrl(returnUrl));
            }
        }

        var model = new AdminLoginInputModel
        {
            ReturnUrl = returnUrl
        };

        return View(model);
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromForm] AdminLoginInputModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var client = _httpClientFactory.CreateClient("BackendApi");

            var loginPayload = new
            {
                email = model.Email.Trim(),
                password = model.Password
            };

            var content = new StringContent(
                JsonSerializer.Serialize(loginPayload),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync("/api/v1/auth/login", content);
            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
                return View(model);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            if (!doc.RootElement.TryGetProperty("accessToken", out var tokenElement))
            {
                ModelState.AddModelError(string.Empty, "Phản hồi xác thực không hợp lệ từ máy chủ.");
                return View(model);
            }

            var accessToken = tokenElement.GetString();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy token hợp lệ.");
                return View(model);
            }

            var profile = JwtAdminHelper.GetProfileFromToken(accessToken);
            if (profile == null || !profile.IsAdmin)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản của bạn không có quyền Quản trị viên (Admin/Moderator).");
                return View(model);
            }

            // Write admin token cookie
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
            };

            Response.Cookies.Append(AuthHeaderHandler.AdminTokenCookieName, accessToken, cookieOptions);
            Response.Cookies.Append(AuthHeaderHandler.AccessTokenCookieName, accessToken, cookieOptions);

            TempData["SuccessMessage"] = $"Chào mừng trở lại, {profile.FullName}!";
            return LocalRedirect(GetSafeReturnUrl(model.ReturnUrl));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Không thể kết nối đến máy chủ xác thực: {ex.Message}");
            return View(model);
        }
    }

    [HttpGet("logout")]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AuthHeaderHandler.AdminTokenCookieName);
        Response.Cookies.Delete(AuthHeaderHandler.AccessTokenCookieName);

        TempData["SuccessMessage"] = "Đã đăng xuất khỏi hệ thống quản trị.";
        return RedirectToAction(nameof(Login));
    }

    private string GetSafeReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) && !returnUrl.Contains("/admin/login", StringComparison.OrdinalIgnoreCase))
        {
            return returnUrl;
        }

        return "/Moderation";
    }
}
