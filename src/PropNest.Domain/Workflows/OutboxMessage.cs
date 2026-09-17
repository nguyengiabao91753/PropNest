namespace PropNest.Domain.Workflows;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public OutboxMessage(string type, string payloadJson, Guid? correlationId = null)
    {
        OutboxId = Guid.NewGuid();
        Type = type;
        PayloadJson = payloadJson;
        CorrelationId = correlationId;
        OccurredOn = DateTimeOffset.UtcNow;
    }

    public Guid OutboxId { get; private set; }
    public DateTimeOffset OccurredOn { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public Guid? CorrelationId { get; private set; }
    public DateTimeOffset? ProcessedOn { get; private set; }
    public int RetryCount { get; private set; }
    public string? Error { get; private set; }

    public void MarkProcessed()
    {
        ProcessedOn = DateTimeOffset.UtcNow;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        RetryCount++;
        Error = error;
    }
}
