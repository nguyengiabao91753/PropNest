namespace PropNest.Domain.Listings;

public sealed class ListingModerationReview
{
    private ListingModerationReview()
    {
    }

    public ListingModerationReview(long listingId, string source, ModerationDecision decision, string? reasonsJson, long? reviewerUserId)
    {
        ListingId = listingId;
        Source = source;
        Decision = decision;
        ReasonsJson = reasonsJson;
        ReviewerUserId = reviewerUserId;
        ReviewedAt = DateTimeOffset.UtcNow;
    }

    public long ReviewId { get; private set; }
    public long ListingId { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public ModerationDecision Decision { get; private set; }
    public string? ReasonsJson { get; private set; }
    public long? ReviewerUserId { get; private set; }
    public DateTimeOffset ReviewedAt { get; private set; }
}
