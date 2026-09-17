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

public sealed record WorkflowDto(
    Guid CorrelationId,
    long ListingId,
    long UserId,
    string PackageCode,
    decimal ChargeAmount,
    WorkflowStatus Status,
    int RetryCount,
    string? ErrorMessage,
    DateTimeOffset UpdatedAt);

public interface IWorkflowRepository
{
    Task<WorkflowInstance?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);

    Task<IdempotencyRequest?> GetIdempotencyRequestAsync(long userId, string key, CancellationToken cancellationToken = default);

    Task AddAsync(WorkflowInstance workflow, CancellationToken cancellationToken = default);

    Task AddIdempotencyRequestAsync(IdempotencyRequest request, CancellationToken cancellationToken = default);

    Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}

public interface IWorkflowService
{
    Task<WorkflowDto> StartPurchaseAsync(PurchasePackageCommand command, CancellationToken cancellationToken = default);

    Task<WorkflowDto?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);
}
