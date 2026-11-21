using PropNest.Application.Abstractions;
using PropNest.Application.DependencyInjection;
using PropNest.Infrastructure.DependencyInjection;
using PropNest.Workflow.Worker.Consumers;
using PropNest.Workflow.Worker.Jobs;
using PropNest.Workflow.Worker.Logging;
using PropNest.Workflow.Worker.Workers;
using Quartz;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}
builder.ConfigureSerilog();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IPurchasePackageSagaConsumer, PurchasePackageSagaConsumer>();
builder.Services.AddHostedService<OutboxDispatcherWorker>();

builder.Services.AddQuartz(q =>
{
    var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Bangkok");

    // 1. Job quét hết hạn tin & hạ gói VIP
    var expirationJobKey = new JobKey(nameof(ListingExpirationJob));
    q.AddJob<ListingExpirationJob>(opts => opts.WithIdentity(expirationJobKey));

    // Quét định kỳ mỗi 1 phút
    q.AddTrigger(opts => opts
        .ForJob(expirationJobKey)
        .WithIdentity($"{nameof(ListingExpirationJob)}-minutely-trigger")
        .WithCronSchedule("0 * * * * ?"));

    // Quét chính xác lúc 00:00:05 (giờ Việt Nam) đầu ngày mới
    q.AddTrigger(opts => opts
        .ForJob(expirationJobKey)
        .WithIdentity($"{nameof(ListingExpirationJob)}-midnight-trigger")
        .WithCronSchedule("5 0 0 * * ?", x => x.InTimeZone(vnTimeZone)));

    // 2. Job đối soát & tự chữa lành dữ liệu giữa SQL Server và Meilisearch
    var reconcileJobKey = new JobKey(nameof(SearchReconciliationJob));
    q.AddJob<SearchReconciliationJob>(opts => opts.WithIdentity(reconcileJobKey));

    // Chạy định kỳ mỗi 15 phút
    q.AddTrigger(opts => opts
        .ForJob(reconcileJobKey)
        .WithIdentity($"{nameof(SearchReconciliationJob)}-periodic-trigger")
        .WithCronSchedule("0 0/15 * * * ?"));

    // Chạy 1 lần sau khi khởi động 10 giây để đồng bộ ban đầu
    q.AddTrigger(opts => opts
        .ForJob(reconcileJobKey)
        .WithIdentity($"{nameof(SearchReconciliationJob)}-startup-trigger")
        .StartAt(DateTimeOffset.UtcNow.AddSeconds(10)));
        .ForJob(jobKey)
        .WithIdentity($"{nameof(ListingExpirationJob)}-trigger")
        .WithCronSchedule("0 0/5 * * * ?"));

    var staleJobKey = new JobKey(nameof(StaleModerationEscalationJob));
    q.AddJob<StaleModerationEscalationJob>(opts => opts.WithIdentity(staleJobKey));
    q.AddTrigger(opts => opts
        .ForJob(staleJobKey)
        .WithIdentity($"{nameof(StaleModerationEscalationJob)}-trigger")
        .WithCronSchedule("0 2/5 * * * ?"));
});

builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});

try
{
    Log.Information("Starting PropNest Workflow Worker host...");
    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "PropNest Workflow Worker terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
