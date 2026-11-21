using PropNest.Application.Abstractions;

namespace PropNest.Workflow.Worker.Workers;

public sealed partial class OutboxDispatcherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcherWorker> _logger;

    public OutboxDispatcherWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox dispatcher worker started.")]
    private partial void LogStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "Processed {MessageCount} outbox message(s).")]
    private partial void LogProcessed(int messageCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox dispatch iteration failed.")]
    private partial void LogDispatchFailed(Exception exception);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted();

        while (!stoppingToken.IsCancellationRequested)
        {
            var dispatched = 0;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatchService>();
                dispatched = await dispatcher.DispatchPendingAsync(stoppingToken);
                if (dispatched > 0)
                {
                    LogProcessed(dispatched);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogDispatchFailed(exception);
            }

            var delayMs = dispatched > 0 ? 1000 : 2000;
            await Task.Delay(TimeSpan.FromMilliseconds(delayMs), stoppingToken);
        }
    }
}
