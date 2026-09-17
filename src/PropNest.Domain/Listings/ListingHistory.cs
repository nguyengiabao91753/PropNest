namespace PropNest.Domain.Listings;

public sealed class ListingHistory
{
    private ListingHistory()
    {
    }

    private ListingHistory(long listingId, long? actorUserId, string actionType, string? snapshotJson, string? deltasJson, string? note)
    {
        ListingId = listingId;
        ActorUserId = actorUserId;
        ActionType = actionType;
        SnapshotJson = snapshotJson;
        DeltasJson = deltasJson;
        Note = note;
        ActionDate = DateTimeOffset.UtcNow;
    }

    public static ListingHistory CreateSnapshot(
        long listingId,
        long? actorUserId,
        string actionType,
        string snapshotJson,
        string? note = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotJson);
        return new ListingHistory(listingId, actorUserId, actionType, snapshotJson, null, note);
    }

    public static ListingHistory CreateDelta(
        long listingId,
        long? actorUserId,
        string actionType,
        string deltasJson,
        string? note = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deltasJson);
        return new ListingHistory(listingId, actorUserId, actionType, null, deltasJson, note);
    }

    public static ListingHistory CreateEvent(
        long listingId,
        long? actorUserId,
        string actionType,
        string? note = null)
    {
        return new ListingHistory(listingId, actorUserId, actionType, null, null, note);
    }

    public long HistoryId { get; private set; }

    public long ListingId { get; private set; }

    public long? ActorUserId { get; private set; }

    public string ActionType { get; private set; } = string.Empty;

    public DateTimeOffset ActionDate { get; private set; }

    public string? SnapshotJson { get; private set; }

    public string? DeltasJson { get; private set; }

    public string? Note { get; private set; }
}
