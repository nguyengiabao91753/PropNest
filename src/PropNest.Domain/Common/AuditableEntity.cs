namespace PropNest.Domain.Common;

public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; protected set; } = DateTimeOffset.UtcNow;

    protected void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
