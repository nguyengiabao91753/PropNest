using Microsoft.EntityFrameworkCore;
using PropNest.Application.Abstractions;
using PropNest.Infrastructure.Persistence;

namespace PropNest.Infrastructure.Services;

public sealed class OutboxDispatchService(PropNestDbContext dbContext, IOutboxMessagePublisher publisher) : IOutboxDispatchService
{
    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await dbContext.OutboxMessages
            .Where(x => x.ProcessedOn == null)
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
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return pending.Count;
    }
}
