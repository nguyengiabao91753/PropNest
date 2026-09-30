using Microsoft.Extensions.DependencyInjection;
using PropNest.Application.Listings;
using PropNest.Application.Wallets;
using PropNest.Application.Workflows;

namespace PropNest.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IListingService, ListingService>();
        services.AddScoped<IWorkflowService, WorkflowService>();

        //bao
        services.AddScoped<IWalletService, WalletService>();

        //phuc


        //tai
        return services;
    }
}
