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
builder.ConfigureSerilog();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IPurchasePackageSagaConsumer, PurchasePackageSagaConsumer>();
builder.Services.AddHostedService<OutboxDispatcherWorker>();

builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey(nameof(ListingExpirationJob));
    q.AddJob<ListingExpirationJob>(opts => opts.WithIdentity(jobKey));

    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity($"{nameof(ListingExpirationJob)}-trigger")
        .WithCronSchedule("0 0/5 * * * ?"));
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
