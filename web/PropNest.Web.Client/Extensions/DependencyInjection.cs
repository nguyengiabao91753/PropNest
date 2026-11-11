using PropNest.Web.Client.Services;
using PropNest.Web.Client.Services.IServices;

namespace Microsoft.Extensions.DependencyInjection;

public static class ClientServiceExtensions
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

        // 1. ApiClientListingServices: Được gắn AuthHeaderHandler để tự động có Bearer token
        services.AddHttpClient<IClientListingService, ApiClientListingServices>(client => client.BaseAddress = baseUri)
                .AddHttpMessageHandler<AuthHeaderHandler>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                });

        // 2. ApiClientAuthService: Không cần token (vì đây là dịch vụ đăng nhập / đăng ký ban đầu)
        services.AddHttpClient<IClientAuthService, ApiClientAuthService>(client => client.BaseAddress = baseUri)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                });

        return services;
    }
}