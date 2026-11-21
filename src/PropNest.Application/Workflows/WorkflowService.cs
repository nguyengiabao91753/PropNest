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
            return await ReplayExistingWorkflowAsync(existing, command.RequestHash, cancellationToken);
        }

        if (await workflowRepository.HasActiveWorkflowForListingAsync(command.ListingId, cancellationToken))
        {
            throw new InvalidOperationException("Tin đăng này đang có một giao dịch mua gói đang xử lý. Vui lòng chờ giao dịch hiện tại hoàn tất.");
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

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Xử lý race condition khi 2 request đồng thời dùng cùng Idempotency-Key
            var raced = await workflowRepository.GetIdempotencyRequestAsync(command.UserId, command.IdempotencyKey, cancellationToken);
            if (raced is not null)
            {
                return await ReplayExistingWorkflowAsync(raced, command.RequestHash, cancellationToken);
            }

            throw;
        }

        return ToDto(workflow, Array.Empty<WorkflowStep>());
    }

    private async Task<WorkflowDto> ReplayExistingWorkflowAsync(
        IdempotencyRequest existing,
        string requestHash,
        CancellationToken cancellationToken)
    {
        if (existing.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Idempotency-Key đã hết hạn. Vui lòng tạo yêu cầu mới.");
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The Idempotency-Key has already been used with a different request.");
        }

        var replay = await workflowRepository.GetByCorrelationIdAsync(existing.CorrelationId, cancellationToken)
            ?? throw new InvalidOperationException("The idempotent request does not have an associated workflow.");
        var steps = await workflowRepository.GetStepsByCorrelationIdAsync(replay.CorrelationId, cancellationToken);
        return ToDto(replay, steps);
    }

    public async Task<WorkflowDto?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        var workflow = await workflowRepository.GetByCorrelationIdAsync(correlationId, cancellationToken);
        if (workflow is null)
        {
            return null;
        }

        var steps = await workflowRepository.GetStepsByCorrelationIdAsync(correlationId, cancellationToken);
        return ToDto(workflow, steps);
    }

    public static string ComputeRequestHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private static WorkflowDto ToDto(WorkflowInstance workflow, IReadOnlyList<WorkflowStep> steps) => new(
        workflow.CorrelationId,
        workflow.ListingId,
        workflow.UserId,
        workflow.PackageCode,
        workflow.ChargeAmount,
        workflow.Status,
        workflow.RetryCount,
        workflow.ErrorMessage,
        workflow.CreatedAt,
        workflow.UpdatedAt,
        steps.Select(s => new WorkflowStepDto(
            s.StepName,
            s.Status,
            s.Attempt,
            s.StartedAt,
            s.FinishedAt,
            s.Error)).ToList());
}
