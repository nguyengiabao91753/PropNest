using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PropNest.Application.Listings;
using Quartz;

namespace PropNest.Workflow.Worker.Jobs;

[DisallowConcurrentExecution]
public sealed partial class ListingExpirationJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ListingExpirationJob> _logger;

    public ListingExpirationJob(IServiceScopeFactory scopeFactory, ILogger<ListingExpirationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ListingExpirationJob started at {Timestamp}. Checking for expired listings...")]
    private partial void LogStarted(DateTimeOffset timestamp);

    [LoggerMessage(Level = LogLevel.Information, Message = "ListingExpirationJob completed. Processed {Count} expired listing(s).")]
    private partial void LogCompleted(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "ListingExpirationJob execution failed.")]
    private partial void LogFailed(Exception exception);

    public async Task Execute(IJobExecutionContext context)
    {
        LogStarted(DateTimeOffset.UtcNow);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var expirationService = scope.ServiceProvider.GetRequiredService<IListingExpirationService>();
            var processed = await expirationService.ExpireListingsAsync(context.CancellationToken);
            LogCompleted(processed);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // Worker is stopping, exit gracefully
        }
        catch (Exception exception)
        {
            LogFailed(exception);
            throw new JobExecutionException(exception, refireImmediately: false);
        }
    }
}
