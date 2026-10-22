using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PropNest.Web.Seller.Helpers;
using PropNest.Web.Seller.Services;

namespace PropNest.Web.Seller.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SellerAuthorizeAttribute : Attribute, IAsyncActionFilter
{
    private const string ClientLoginUrl = "https://localhost:7129/login";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var hasToken = httpContext.Request.Cookies.TryGetValue(AuthHeaderHandler.TokenCookieName, out var token);

        if (!hasToken || string.IsNullOrWhiteSpace(token))
        {
            var returnUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.Path}{httpContext.Request.QueryString}";
            context.Result = new RedirectResult($"{ClientLoginUrl}?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        var profile = JwtUserHelper.GetProfileFromToken(token);
        if (profile == null || !profile.IsSeller)
        {
            // Nếu không phải là Seller (ví dụ Customer hoặc Admin cố vào Seller portal)
            var returnUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.Path}{httpContext.Request.QueryString}";
            context.Result = new RedirectResult($"{ClientLoginUrl}?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        // Lưu userProfile vào HttpContext.Items để View hoặc Controller dễ dàng truy xuất
        httpContext.Items["UserProfile"] = profile;

        await next();
    }
}
