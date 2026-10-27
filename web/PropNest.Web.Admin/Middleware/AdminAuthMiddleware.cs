using PropNest.Web.Admin.Helpers;
using PropNest.Web.Admin.Services.Handler;

namespace PropNest.Web.Admin.Middleware;

public sealed class AdminAuthMiddleware
{
    private readonly RequestDelegate _next;

    public AdminAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Skip static files & assets
        if (IsStaticAsset(path))
        {
            await _next(context);
            return;
        }

        // Allow login and logout actions
        if (path.StartsWith("/admin/login", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/admin/logout", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Check token from cookies or Authorization header
        string? token = null;
        if (context.Request.Cookies.TryGetValue(AuthHeaderHandler.AdminTokenCookieName, out var adminToken) &&
            !string.IsNullOrWhiteSpace(adminToken))
        {
            token = adminToken;
        }
        else if (context.Request.Cookies.TryGetValue(AuthHeaderHandler.AccessTokenCookieName, out var accessToken) &&
                 !string.IsNullOrWhiteSpace(accessToken))
        {
            token = accessToken;
        }
        else if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var headerValue = authHeader.ToString();
            if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = headerValue["Bearer ".Length..].Trim();
            }
        }

        var profile = JwtAdminHelper.GetProfileFromToken(token);
        var isAuthenticated = profile != null && profile.IsAdmin;

        // If authenticated, store profile and proceed
        if (isAuthenticated)
        {
            context.Items["AdminProfile"] = profile;
            await _next(context);
            return;
        }

        // Unauthenticated handling
        // If it's an API / AJAX request
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
            context.Request.Headers.Accept.Any(a => a != null && a.Contains("application/json", StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"success\":false,\"message\":\"Yêu cầu quyền quản trị viên Admin/Moderator. Vui lòng đăng nhập lại.\"}");
            return;
        }

        // Redirect to /admin/login
        var returnUrl = $"{context.Request.Path}{context.Request.QueryString}";
        var loginUrl = $"/admin/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        context.Response.Redirect(loginUrl);
    }

    private static bool IsStaticAsset(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        return path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".woff", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".map", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase);
    }
}
