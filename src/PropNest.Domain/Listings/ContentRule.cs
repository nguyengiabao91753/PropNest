namespace PropNest.Domain.Listings;

public sealed class ContentRule
{
    private ContentRule()
    {
    }

    public ContentRule(
        string ruleType,
        string pattern,
        string severity,
        string? description = null,
        bool isActive = true,
        long ruleId = 0,
        DateTimeOffset? updatedAt = null)
    {
        RuleId = ruleId;
        ApplyRule(ruleType, pattern, severity, description, isActive, updatedAt);
    }

    public long RuleId { get; private set; }

    public string RuleType { get; private set; } = string.Empty;

    public string Pattern { get; private set; } = string.Empty;

    public string Severity { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public void Update(string ruleType, string pattern, string severity, string? description, bool isActive)
    {
        ApplyRule(ruleType, pattern, severity, description, isActive, DateTimeOffset.UtcNow);
    }

    private void ApplyRule(
        string ruleType,
        string pattern,
        string severity,
        string? description,
        bool isActive,
        DateTimeOffset? updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleType);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(severity);

        RuleType = ruleType.Trim();
        Pattern = pattern.Trim();
        Severity = severity.Trim();
        Description = description?.Trim();
        IsActive = isActive;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }
}
