using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PropNest.Application.Abstractions;
using PropNest.Domain.Workflows;

namespace PropNest.Application.Workflows;

public sealed class WorkflowService(IWorkflowRepository workflowRepository, IUnitOfWork unitOfWork) : IWorkflowService
{
    public async Task<WorkflowDto> StartPurchaseAsync(PurchasePackageCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            throw new ArgumentException("Idempotency-Key is required.", nameof(command));
        }

        var existing = await workflowRepository.GetIdempotencyRequestAsync(command.UserId, command.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, command.RequestHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The Idempotency-Key has already been used with a different request.");
            }

            var replay = await workflowRepository.GetByCorrelationIdAsync(existing.CorrelationId, cancellationToken)
                ?? throw new InvalidOperationException("The idempotent request does not have an associated workflow.");
            return ToDto(replay);
        }

        var correlationId = Guid.NewGuid();
        var workflow = new WorkflowInstance(correlationId, command.ListingId, command.UserId, command.PackageCode, command.ChargeAmount);
        var request = new IdempotencyRequest(
            command.IdempotencyKey,
            command.UserId,
            command.RequestPath,
            command.RequestHash,
            correlationId,
            DateTimeOffset.UtcNow.AddHours(24));
        var payload = JsonSerializer.Serialize(new
        {
            workflow.CorrelationId,
            workflow.ListingId,
            workflow.UserId,
            workflow.PackageCode,
            workflow.ChargeAmount
        });

        await workflowRepository.AddAsync(workflow, cancellationToken);
        await workflowRepository.AddIdempotencyRequestAsync(request, cancellationToken);
        await workflowRepository.AddOutboxMessageAsync(new OutboxMessage("PurchasePackageRequested", payload, correlationId), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(workflow);
    }

    public async Task<WorkflowDto?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        var workflow = await workflowRepository.GetByCorrelationIdAsync(correlationId, cancellationToken);
        return workflow is null ? null : ToDto(workflow);
    }

    public static string ComputeRequestHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private static WorkflowDto ToDto(WorkflowInstance workflow) => new(
        workflow.CorrelationId,
        workflow.ListingId,
        workflow.UserId,
        workflow.PackageCode,
        workflow.ChargeAmount,
        workflow.Status,
        workflow.RetryCount,
        workflow.ErrorMessage,
        workflow.UpdatedAt);
}
