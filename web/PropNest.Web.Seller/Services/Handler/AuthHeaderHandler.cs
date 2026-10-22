using System.Net.Http.Headers;

namespace PropNest.Web.Seller.Services;

public sealed class AuthHeaderHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public const string TokenCookieName = "propnest_access_token";

    public AuthHeaderHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        // Đọc JWT token từ Cookie nếu người dùng đã đăng nhập
        if (httpContext != null &&
            httpContext.Request.Cookies.TryGetValue(TokenCookieName, out var token) &&
            !string.IsNullOrWhiteSpace(token))
        {
            // Tự động gắn Authorization: Bearer <token> vào request
            if (request.Headers.Authorization == null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}