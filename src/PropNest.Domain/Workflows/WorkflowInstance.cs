using PropNest.Domain.Common;

namespace PropNest.Domain.Workflows;

public enum WorkflowStatus
{
    Started,
    DeductingWallet,
    WalletDeducted,
    UpgradingListing,
    RecordingHistory,
    Completed,
    Compensating,
    Compensated,
    Failed
}

public sealed class WorkflowInstance : AuditableEntity
{
    private WorkflowInstance()
    {
    }

    public WorkflowInstance(Guid correlationId, long listingId, long userId, string packageCode, decimal chargeAmount)
    {
        CorrelationId = correlationId;
        ListingId = listingId;
        UserId = userId;
        PackageCode = packageCode;
        ChargeAmount = chargeAmount;
        Status = WorkflowStatus.Started;
    }

    public Guid CorrelationId { get; private set; }
    public long ListingId { get; private set; }
    public long UserId { get; private set; }
    public string PackageCode { get; private set; } = string.Empty;
    public decimal ChargeAmount { get; private set; }
    public WorkflowStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void MoveTo(WorkflowStatus status, string? errorMessage = null)
    {
        Status = status;
        ErrorMessage = errorMessage;
        Touch();
    }

    public void IncrementRetry()
    {
        RetryCount++;
        Touch();
    }
}
