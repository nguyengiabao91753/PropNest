using Microsoft.Extensions.Logging;
using PropNest.Domain.Workflows;

namespace PropNest.Infrastructure.Services;

public sealed partial class LoggingOutboxMessagePublisher : IOutboxMessagePublisher
{
    private readonly ILogger<LoggingOutboxMessagePublisher> _logger;

    public LoggingOutboxMessagePublisher(ILogger<LoggingOutboxMessagePublisher> logger)
    {
        _logger = logger;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Outbox event {EventType} with message {OutboxId} and correlation {CorrelationId} is ready for transport.")]
    private partial void LogOutboxReady(string eventType, Guid outboxId, Guid? correlationId);

    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        LogOutboxReady(message.Type, message.OutboxId, message.CorrelationId);
        return Task.CompletedTask;
    }
}
