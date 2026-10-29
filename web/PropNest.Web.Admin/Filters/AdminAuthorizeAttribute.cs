using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PropNest.Web.Admin.Helpers;
using PropNest.Web.Admin.Services.Handler;

namespace PropNest.Web.Admin.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminAuthorizeAttribute : Attribute, IAsyncActionFilter
{
    private const string AdminLoginUrl = "/admin/login";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;

        // Check if AdminProfile already resolved by Middleware
        if (httpContext.Items["AdminProfile"] is AdminUserProfile profile && profile.IsAdmin)
        {
            await next();
            return;
        }

        string? token = null;
        if (httpContext.Request.Cookies.TryGetValue(AuthHeaderHandler.AdminTokenCookieName, out var adminToken) &&
            !string.IsNullOrWhiteSpace(adminToken))
        {
            token = adminToken;
        }
        else if (httpContext.Request.Cookies.TryGetValue(AuthHeaderHandler.AccessTokenCookieName, out var accessToken) &&
                 !string.IsNullOrWhiteSpace(accessToken))
        {
            token = accessToken;
        }

        var resolvedProfile = JwtAdminHelper.GetProfileFromToken(token);
        if (resolvedProfile == null || !resolvedProfile.IsAdmin)
        {
            var returnUrl = $"{httpContext.Request.Path}{httpContext.Request.QueryString}";
            context.Result = new RedirectResult($"{AdminLoginUrl}?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        httpContext.Items["AdminProfile"] = resolvedProfile;
        await next();
    }
}
