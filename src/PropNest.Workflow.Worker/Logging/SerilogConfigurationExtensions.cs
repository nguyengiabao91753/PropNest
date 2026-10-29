using Serilog;

namespace PropNest.Workflow.Worker.Logging;

/// <summary>
/// Provides Serilog configuration for the Workflow Worker host.
/// Uses the same JSON format as PropNest.Api for consistent structured logging.
/// WorkflowId / CorrelationId can be pushed via Log.ForContext("WorkflowId", correlationId)
/// inside job/consumer code and will be emitted automatically into each log event.
/// </summary>
public static class SerilogConfigurationExtensions
{
    private const string LogFilePathTemplate = "logs/propnest-.json";

    public static IHostApplicationBuilder ConfigureSerilog(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSerilog((services, configuration) =>
        {
            var jsonFormatter = new PropNestJsonFormatter();

            configuration
                .ReadFrom.Configuration(builder.Configuration)
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
