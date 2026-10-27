using PropNest.Web.Admin.Services;
using PropNest.Web.Admin.Services.Handler;
using PropNest.Web.Admin.Services.IServices;

namespace PropNest.Web.Admin.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddBackendApiClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var baseUrl = configuration["ServiceUrls:BackendApi"] ?? "https://localhost:65147";
        var baseUri = new Uri(baseUrl);

        // Đăng ký HttpContextAccessor và AuthHeaderHandler
        services.AddHttpContextAccessor();
        services.AddTransient<AuthHeaderHandler>();

        static HttpMessageHandler CreateDevHttpClientHandler() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        // 1. Named client cho AuthController
        services.AddHttpClient("BackendApi", client =>
        {
            client.BaseAddress = baseUri;
        })
        .ConfigurePrimaryHttpMessageHandler(CreateDevHttpClientHandler);

        // 2. Typed client cho IAdminListingService (gắn AuthHeaderHandler tự động kèm Bearer Token)
        services.AddHttpClient<IAdminListingService, ApiAdminListingService>(client =>
        {
            client.BaseAddress = baseUri;
        })
        .AddHttpMessageHandler<AuthHeaderHandler>()
        .ConfigurePrimaryHttpMessageHandler(CreateDevHttpClientHandler);

        return services;
    }
}
