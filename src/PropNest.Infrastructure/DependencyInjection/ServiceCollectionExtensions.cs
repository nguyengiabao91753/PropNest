using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;
using PropNest.Application.Workflows;
using PropNest.Infrastructure.Persistence;
using PropNest.Infrastructure.Persistence.Repositories;
using PropNest.Infrastructure.Services;

namespace PropNest.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PropNest")
            ?? throw new InvalidOperationException("Connection string 'PropNest' is required.");

        services.AddDbContext<PropNestDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<PropNestDbContext>());
        services.AddScoped<IListingRepository, ListingRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IOutboxDispatchService, OutboxDispatchService>();
        services.AddSingleton<IOutboxMessagePublisher, LoggingOutboxMessagePublisher>();
        services.AddHealthChecks().AddDbContextCheck<PropNestDbContext>("sqlserver");
        return services;
    }
}
