using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropNest.Application.Abstractions;
using PropNest.Infrastructure.Persistence;

namespace PropNest.Infrastructure.Services;

public sealed partial class OutboxDispatchService(
    PropNestDbContext dbContext,
    IOutboxMessagePublisher publisher,
    ILogger<OutboxDispatchService>? logger = null) : IOutboxDispatchService
{
    public const int MaxRetryCount = 5;

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await dbContext.OutboxMessages
            .Where(x => x.ProcessedOn == null && x.RetryCount < MaxRetryCount)
            .OrderBy(x => x.OccurredOn)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);
                message.MarkProcessed();
            }
            catch (Exception exception)
            {
                message.MarkFailed(exception.Message);
                if (logger is not null)
                {
                    if (message.RetryCount >= MaxRetryCount)
                    {
                        LogPoisonMessage(logger, message.OutboxId, message.Type, message.RetryCount, exception.Message);
                    }
                    else
                    {
                        LogDispatchFailed(logger, message.OutboxId, message.Type, message.RetryCount, exception.Message);
                    }
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return pending.Count;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "OutboxDispatchService: Failed to dispatch OutboxId={OutboxId} ({Type}), RetryCount={RetryCount}: {Error}")]
    private static partial void LogDispatchFailed(ILogger logger, Guid outboxId, string type, int retryCount, string error);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "OutboxDispatchService: OutboxId={OutboxId} ({Type}) exceeded max retries ({RetryCount}) and was quarantined to avoid blocking newer messages: {Error}")]
    private static partial void LogPoisonMessage(ILogger logger, Guid outboxId, string type, int retryCount, string error);
}
