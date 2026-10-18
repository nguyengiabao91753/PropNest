using PropNest.Web.Seller.Services;
using PropNest.Web.Seller.Services.IServices;

namespace PropNest.Web.Seller.Extensions
{
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

            // 1. ApiSellerPropertyService: Được gắn AuthHeaderHandler để tự động có Bearer token
            services.AddHttpClient<ISellerPropertyService, ApiSellerPropertyService>(client => client.BaseAddress = baseUri)
                    .AddHttpMessageHandler<AuthHeaderHandler>();

            return services;
        }
    }
}
