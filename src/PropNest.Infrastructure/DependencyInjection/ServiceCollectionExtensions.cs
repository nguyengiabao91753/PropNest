using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;
using PropNest.Application.Wallets;
using PropNest.Application.Workflows;
using PropNest.Infrastructure.Persistence;
using PropNest.Infrastructure.Persistence.Repositories;
using PropNest.Infrastructure.Services;
using MassTransit;
using PropNest.Infrastructure.Workflows;

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
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IOutboxMessagePublisher, LoggingOutboxMessagePublisher>();

        //bao
        services.AddScoped<IWalletRepository, WalletRepository>();
        //phuc

        //tai



        // CẤU HÌNH MASSTRANSIT + RABBITMQ
        services.AddMassTransit(busConfig =>
        {

            busConfig.SetKebabCaseEndpointNameFormatter();

            busConfig.UsingRabbitMq((context, cfg) =>
            {
                var host = configuration["RabbitMq:Host"] ?? "localhost";
                var port = ushort.TryParse(configuration["RabbitMq:Port"], out var p) ? p : (ushort)5672;
                var virtualHost = configuration["RabbitMq:VirtualHost"] ?? "/";
                var username = configuration["RabbitMq:Username"] ?? "guest";
                var password = configuration["RabbitMq:Password"] ?? "guest";
                cfg.Host(host, port, virtualHost, h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.ConfigureEndpoints(context);
            });

            // đăng ký saga
            busConfig.AddSagaStateMachine<PurchasePackageSaga, PurchasePackageSagaState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                    r.ExistingDbContext<PropNestDbContext>();
                    r.UseSqlServer();
                });
        });
       
        services.AddHealthChecks()
            .AddDbContextCheck<PropNestDbContext>("sqlserver");
        return services;
    }
}
