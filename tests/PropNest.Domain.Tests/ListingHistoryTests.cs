using PropNest.Domain.Listings;

namespace PropNest.Domain.Tests;

public sealed class ListingHistoryTests
{
    [Fact]
    public void CreateSnapshotStoresOnlySnapshotPayload()
    {
        var history = ListingHistory.CreateSnapshot(10, 20, "Create", "{\"title\":\"Apartment\"}");

        Assert.Equal(10, history.ListingId);
        Assert.Equal(20, history.ActorUserId);
        Assert.Equal("Create", history.ActionType);
        Assert.Equal("{\"title\":\"Apartment\"}", history.SnapshotJson);
        Assert.Null(history.DeltasJson);
    }

    [Fact]
    public void CreateDeltaStoresOnlyDeltaPayload()
    {
        var history = ListingHistory.CreateDelta(10, 20, "UpdateContent", "[{\"fieldName\":\"Price\"}]");

        Assert.Null(history.SnapshotJson);
        Assert.Equal("[{\"fieldName\":\"Price\"}]", history.DeltasJson);
    }

    [Fact]
    public void CreateEventStoresNoPayload()
    {
        var history = ListingHistory.CreateEvent(10, 20, "Submit");

        Assert.Null(history.SnapshotJson);
        Assert.Null(history.DeltasJson);
    }
}
