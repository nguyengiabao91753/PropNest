using Serilog;
using Serilog.Events;

namespace PropNest.Api.Logging;

/// <summary>
/// Provides a shared Serilog configuration for structured JSON logging.
/// Output: PropNestJsonFormatter to console + rolling daily JSON file.
/// Enriched with MachineName, ThreadId, and CorrelationId pushed by CorrelationIdMiddleware.
/// </summary>
public static class SerilogConfigurationExtensions
{
    private const string LogFilePathTemplate = "logs/propnest-.json";

    public static WebApplicationBuilder ConfigureSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            var jsonFormatter = new PropNestJsonFormatter();

            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .WriteTo.Console(formatter: jsonFormatter)
                .WriteTo.File(
                    formatter: jsonFormatter,
                    path: LogFilePathTemplate,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 31,
                    shared: true);
        });

        return builder;
    }
}
