using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PropNest.Web.Seller.Helpers;
using PropNest.Web.Seller.Services;

namespace PropNest.Web.Seller.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SellerAuthorizeAttribute : Attribute, IAsyncActionFilter
{
    private const string DefaultClientBaseUrl = "https://localhost:7129";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var hasToken = httpContext.Request.Cookies.TryGetValue(AuthHeaderHandler.TokenCookieName, out var token);
        var clientLoginUrl = ResolveClientLoginUrl(httpContext);

        if (!hasToken || string.IsNullOrWhiteSpace(token))
        {
            var returnUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.Path}{httpContext.Request.QueryString}";
            context.Result = new RedirectResult($"{clientLoginUrl}?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        var profile = JwtUserHelper.GetProfileFromToken(token);
        if (profile == null)
        {
            var returnUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.Path}{httpContext.Request.QueryString}";
            context.Result = new RedirectResult($"{clientLoginUrl}?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        httpContext.Items["UserProfile"] = profile;

        await next();
    }

    private static string ResolveClientLoginUrl(HttpContext httpContext)
    {
        var configuration = httpContext.RequestServices.GetService<IConfiguration>();
        var clientBaseUrl = configuration?["ServiceUrls:WebClient"];
        if (string.IsNullOrWhiteSpace(clientBaseUrl))
        {
            clientBaseUrl = DefaultClientBaseUrl;
        }

        return $"{clientBaseUrl.TrimEnd('/')}/login";
    }
}
