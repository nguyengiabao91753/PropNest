using System.Net.Http.Headers;

namespace PropNest.Web.Admin.Services.Handler;

public sealed class AuthHeaderHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public const string AdminTokenCookieName = "propnest_admin_token";
    public const string AccessTokenCookieName = "propnest_access_token";

    public AuthHeaderHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext != null)
        {
            string? token = null;

            if (httpContext.Request.Cookies.TryGetValue(AdminTokenCookieName, out var adminToken) &&
                !string.IsNullOrWhiteSpace(adminToken))
            {
                token = adminToken;
            }
            else if (httpContext.Request.Cookies.TryGetValue(AccessTokenCookieName, out var accessToken) &&
                     !string.IsNullOrWhiteSpace(accessToken))
            {
                token = accessToken;
            }

            if (!string.IsNullOrWhiteSpace(token) && request.Headers.Authorization == null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
