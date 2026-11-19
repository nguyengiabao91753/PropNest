using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PropNest.Application.Listings;
using Quartz;

namespace PropNest.Workflow.Worker.Jobs;

[DisallowConcurrentExecution]
public sealed partial class StaleModerationEscalationJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StaleModerationEscalationJob> _logger;

    public StaleModerationEscalationJob(
        IServiceScopeFactory scopeFactory,
        ILogger<StaleModerationEscalationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "StaleModerationEscalationJob started at {Timestamp}.")]
    private partial void LogStarted(DateTimeOffset timestamp);

    [LoggerMessage(Level = LogLevel.Information, Message = "StaleModerationEscalationJob escalated {Count} stale pending listing(s) to Admin ManualReview.")]
    private partial void LogCompleted(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "StaleModerationEscalationJob execution failed.")]
    private partial void LogFailed(Exception exception);

    public async Task Execute(IJobExecutionContext context)
    {
        LogStarted(DateTimeOffset.UtcNow);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var watchdog = scope.ServiceProvider.GetRequiredService<IListingModerationWatchdogService>();
            var escalated = await watchdog.EscalateStalePendingListingsAsync(cancellationToken: context.CancellationToken);
            if (escalated > 0)
            {
                LogCompleted(escalated);
            }
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // Graceful worker shutdown
        }
        catch (Exception ex)
        {
            LogFailed(ex);
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}
