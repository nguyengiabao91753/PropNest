using Microsoft.EntityFrameworkCore;
using PropNest.Application.Workflows;
using PropNest.Domain.Workflows;

namespace PropNest.Infrastructure.Persistence.Repositories;

public sealed class WorkflowRepository(PropNestDbContext dbContext) : IWorkflowRepository
{
    public Task<WorkflowInstance?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default) =>
        dbContext.WorkflowInstances.SingleOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken);

    public Task<IdempotencyRequest?> GetIdempotencyRequestAsync(long userId, string key, CancellationToken cancellationToken = default) =>
        dbContext.IdempotencyRequests.SingleOrDefaultAsync(x => x.UserId == userId && x.Key == key, cancellationToken);

    public Task AddAsync(WorkflowInstance workflow, CancellationToken cancellationToken = default) =>
        dbContext.WorkflowInstances.AddAsync(workflow, cancellationToken).AsTask();

    public Task AddIdempotencyRequestAsync(IdempotencyRequest request, CancellationToken cancellationToken = default) =>
        dbContext.IdempotencyRequests.AddAsync(request, cancellationToken).AsTask();

    public Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
        dbContext.OutboxMessages.AddAsync(message, cancellationToken).AsTask();

    public async Task<IReadOnlyList<WorkflowStep>> GetStepsByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        var steps = await dbContext.WorkflowSteps
            .Where(x => x.CorrelationId == correlationId)
            .OrderBy(x => x.StepId)
            .ToListAsync(cancellationToken);

        return steps.OrderBy(x => x.StartedAt).ThenBy(x => x.StepId).ToList();
    }

    public Task AddStepAsync(WorkflowStep workflowStep, CancellationToken cancellationToken = default) =>
        dbContext.WorkflowSteps.AddAsync(workflowStep, cancellationToken).AsTask();
}
