using PropNest.Domain.Listings;

namespace PropNest.Domain.Tests;

public sealed class ListingTests
{
    [Fact]
    public void SubmitForModerationMovesDraftToPendingModeration()
    {
        var listing = CreateListing();

        listing.SubmitForModeration();

        Assert.Equal(ListingStatus.PendingModeration, listing.Status);
        Assert.Equal(ModerationDecision.Pending, listing.ModerationDecision);
    }

    [Fact]
    public void ApproveRequiresAListingPendingModeration()
    {
        var listing = CreateListing();

        Assert.Throws<InvalidOperationException>(() => listing.Approve(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7)));
    }

    private static Listing CreateListing() => new(
        1,
        "Apartment for sale",
        "A valid property listing.",
        "Apartment",
        "Sale",
        4_500_000_000m,
        75m,
        "Ho Chi Minh City",
        "District 7",
        "Tan Phu",
        "1 Nguyen Van Linh");
}
