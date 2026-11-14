namespace PropNest.Domain.Listings;

public sealed class AmeModerationResult
{
    private AmeModerationResult()
    {
    }

    public AmeModerationResult(
        long listingId,
        Guid correlationId,
        ModerationDecision decision,
        string? scoreJson,
        string? reasonsJson,
        string? warningsJson,
        string? modelInfo,
        DateTimeOffset? checkedAt = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("CorrelationId cannot be empty.", nameof(correlationId));
        }

        ListingId = listingId;
        CorrelationId = correlationId;
        Decision = decision;
        ScoreJson = scoreJson;
        ReasonsJson = reasonsJson;
        WarningsJson = warningsJson;
        ModelInfo = modelInfo?.Trim();
        CheckedAt = checkedAt ?? DateTimeOffset.UtcNow;
    }

    public long ResultId { get; private set; }

    public long ListingId { get; private set; }

    public Guid CorrelationId { get; private set; }

    public ModerationDecision Decision { get; private set; }

    public string? ScoreJson { get; private set; }

    public string? ReasonsJson { get; private set; }

    public string? WarningsJson { get; private set; }

    public string? ModelInfo { get; private set; }

    public DateTimeOffset CheckedAt { get; private set; }
}
