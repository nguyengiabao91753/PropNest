using PropNest.Domain.Workflows;

namespace PropNest.Application.Workflows;

public sealed record PurchasePackageCommand(
    long ListingId,
    long UserId,
    string PackageCode,
    decimal ChargeAmount,
    string IdempotencyKey,
    string RequestPath,
    string RequestHash);

public sealed record WorkflowStepDto(
    string StepName,
    string Status,
    int Attempt,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string? Error);

public sealed record WorkflowDto(
    Guid CorrelationId,
    long ListingId,
    long UserId,
    string PackageCode,
    decimal ChargeAmount,
    WorkflowStatus Status,
    int RetryCount,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WorkflowStepDto> Steps);

public interface IWorkflowRepository
{
    Task<WorkflowInstance?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);

    Task<IdempotencyRequest?> GetIdempotencyRequestAsync(long userId, string key, CancellationToken cancellationToken = default);

    Task AddAsync(WorkflowInstance workflow, CancellationToken cancellationToken = default);

    Task AddIdempotencyRequestAsync(IdempotencyRequest request, CancellationToken cancellationToken = default);

    Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowStep>> GetStepsByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);

    Task AddStepAsync(WorkflowStep workflowStep, CancellationToken cancellationToken = default);
}

public interface IWorkflowService
{
    Task<WorkflowDto> StartPurchaseAsync(PurchasePackageCommand command, CancellationToken cancellationToken = default);

    Task<WorkflowDto?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);
}
