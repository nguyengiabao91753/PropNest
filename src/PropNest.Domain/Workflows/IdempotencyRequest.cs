using PropNest.Domain.Common;

namespace PropNest.Domain.Workflows;

public sealed class IdempotencyRequest : AuditableEntity
{
    private IdempotencyRequest()
    {
    }

    public IdempotencyRequest(string key, long userId, string requestPath, string requestHash, Guid correlationId, DateTimeOffset expiresAt)
    {
        Key = key;
        UserId = userId;
        RequestPath = requestPath;
        RequestHash = requestHash;
        CorrelationId = correlationId;
        ExpiresAt = expiresAt;
        Status = "Pending";
    }

    public long RequestId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public long UserId { get; private set; }
    public string RequestPath { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public Guid CorrelationId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public int? ResponseStatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public void Complete(int responseStatusCode, string? responseBody)
    {
        Status = "Completed";
        ResponseStatusCode = responseStatusCode;
        ResponseBody = responseBody;
        Touch();
    }
}
