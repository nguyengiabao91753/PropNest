using PropNest.Domain.Workflows;

namespace PropNest.Infrastructure.Services;

public interface IOutboxMessagePublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
