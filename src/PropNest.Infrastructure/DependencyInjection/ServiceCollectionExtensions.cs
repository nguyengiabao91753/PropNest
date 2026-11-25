using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;
using PropNest.Application.Listings.Search;
using PropNest.Application.Wallets;
using PropNest.Application.Workflows;
using PropNest.Infrastructure.Consumers;
using PropNest.Infrastructure.Filters;
using PropNest.Infrastructure.Persistence;
using PropNest.Infrastructure.Persistence.Repositories;
using PropNest.Infrastructure.Services;
using PropNest.Infrastructure.Services.Search;
using PropNest.Infrastructure.Workflows;
using Meilisearch;
using Microsoft.Extensions.Options;

namespace PropNest.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool registerConsumers = true)
    {
        var connectionString = configuration.GetConnectionString("PropNest")
            ?? throw new InvalidOperationException("Connection string 'PropNest' is required.");

        services.AddMemoryCache();
        services.AddDbContext<PropNestDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<PropNestDbContext>());
        services.AddScoped<IListingRepository, ListingRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IOutboxDispatchService, OutboxDispatchService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IOutboxMessagePublisher, MassTransitOutboxPublisher>();
        services.AddScoped<IPhotoService, CloudinaryPhotoService>();

        //bao
        services.AddScoped<IWalletRepository, WalletRepository>();
        //phuc

        //tai

        // Cấu hình Vietnam Address API (provinces.open-api.vn v1/v2)
        services.Configure<VietnamAddressOptions>(configuration.GetSection(VietnamAddressOptions.SectionName));
        services.AddHttpClient<IVietnamAddressService, VietnamAddressService>((_, client) =>
        {
            var vnOptions = configuration.GetSection(VietnamAddressOptions.SectionName).Get<VietnamAddressOptions>() ?? new VietnamAddressOptions();
            client.BaseAddress = new Uri(vnOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(vnOptions.TimeoutSeconds > 0 ? vnOptions.TimeoutSeconds : 15);
        });

        // Cấu hình Ollama Local AI gợi ý mô tả tin đăng
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));
        services.AddHttpClient<IListingDescriptionGenerator, OllamaListingDescriptionGenerator>((_, client) =>
        {
            var ollamaOptions = configuration.GetSection(OllamaOptions.SectionName).Get<OllamaOptions>() ?? new OllamaOptions();
            client.BaseAddress = new Uri(ollamaOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(ollamaOptions.TimeoutSeconds > 0 ? ollamaOptions.TimeoutSeconds : 25);
        });

        // Cấu hình AME Options & Typed HttpClient gọi sang Python FastAPI
        services.Configure<AmeOptions>(configuration.GetSection(AmeOptions.SectionName));
        services.AddHttpClient<IAmeModerationClient, AmeModerationClient>((serviceProvider, client) =>
        {
            var ameOptions = configuration.GetSection(AmeOptions.SectionName).Get<AmeOptions>() ?? new AmeOptions();
            client.BaseAddress = new Uri(ameOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(ameOptions.TimeoutSeconds > 0 ? ameOptions.TimeoutSeconds : 15);
        });

        // Cấu hình Meilisearch & Index Initializer
        services.Configure<MeilisearchOptions>(configuration.GetSection(MeilisearchOptions.SectionName));
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MeilisearchOptions>>().Value;
            // Dùng timeout 30s cho các tác vụ quản trị/đồng bộ (IndexInitializer, SearchReconciliationJob);
            // truy vấn tìm kiếm tự áp đặt RequestTimeoutMs (mặc định 2000ms) qua CancellationTokenSource riêng.
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(options.Url),
                Timeout = TimeSpan.FromSeconds(30)
            };
            return new MeilisearchClient(httpClient, options.MasterKey);
        });
        if (registerConsumers)
        {
            services.AddHostedService<MeilisearchIndexInitializer>();
        }
        services.AddScoped<IMeilisearchListingSearchService, MeilisearchListingSearchService>();

        // CẤU HÌNH MASSTRANSIT + RABBITMQ
        services.AddMassTransit(busConfig =>
        {
            busConfig.SetKebabCaseEndpointNameFormatter();

            if (registerConsumers)
            {
                // đăng ký saga (chỉ chạy trên Workflow.Worker để tránh tranh chấp consumer với API)
                busConfig.AddSagaStateMachine<PurchasePackageSaga, PurchasePackageSagaState>()
                    .EntityFrameworkRepository(r =>
                    {
                        r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                        r.ExistingDbContext<PropNestDbContext>();
                        r.UseSqlServer();
                    });

                // đăng ký consumers
                busConfig.AddConsumer<DeductWalletConsumer>();
                busConfig.AddConsumer<UpgradeListingConsumer>();
                busConfig.AddConsumer<RefundWalletConsumer>();
                busConfig.AddConsumer<AmeModerationConsumer>();
                busConfig.AddConsumer<SyncListingSearchConsumer>(typeof(SyncListingSearchConsumerDefinition));
            }

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
                if (registerConsumers)
                {
                    cfg.UseConsumeFilter(typeof(InboxDeduplicationFilter<>), context);
                    cfg.ConfigureEndpoints(context);
                }
            });
        });

        services.AddHealthChecks()
            .AddDbContextCheck<PropNestDbContext>("sqlserver");
        return services;
    }
}
