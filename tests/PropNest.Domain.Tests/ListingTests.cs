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

    //Nộp lại tin đăng đã bị từ chối để đưa về trạng thái đang chờ duyệt
    [Fact]
    public void SubmitForModerationFromRejectedShouldMovesToPendingModeration()
    {
        var listing = CreateRejectedListing();
        listing.SubmitForModeration();
        Assert.Equal(ListingStatus.PendingModeration, listing.Status);
        Assert.Equal(ModerationDecision.Pending, listing.ModerationDecision);
    }

    //Nộp lại tin đăng đã bị ẩn để đưa về trạng thái đang chờ duyệt
    [Fact]
    public void SubmitForModerationFromHiddenShouldMoveToPendingModeration()
    {
        var listing = CreateHiddenListing();
        listing.SubmitForModeration();
        Assert.Equal(ListingStatus.PendingModeration, listing.Status);
        Assert.Equal(ModerationDecision.Pending, listing.ModerationDecision);
    }

    //Duyệt tin hợp lệ -> Published
    [Fact]
    public void ApproveFromPendingModerationShouldMoveToPublished()
    {
        var listing = CreatePendingListing();
        listing.Approve(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7));
        Assert.Equal(ListingStatus.Published, listing.Status);
        Assert.Equal(ModerationDecision.Approved, listing.ModerationDecision);
    }

    //Từ chối tin hợp lệ -> Rejected
    [Fact]
    public void RejectFromPendingModerationShouldMoveToRejected()
    {
        var listing = CreatePendingListing();
        listing.Reject();
        Assert.Equal(ListingStatus.Rejected, listing.Status);
        Assert.Equal(ModerationDecision.Rejected, listing.ModerationDecision);
    }

    //Ẩn tin đăng đang hiển thị-> Hidden
    [Fact]
    public void HideFromPublishedShouldMoveToHidden()
    {
        var listing = CreatePublishedListing();
        listing.Hide();
        Assert.Equal(ListingStatus.Hidden, listing.Status);
    }

    //Nộp duyệt tin khi tin đã Published -> InvalidOperationException
    [Fact]
    public void SubmitForModerationFromPublishedShouldThrowInvalidOperationException()
    {
        var listing = CreatePublishedListing();
        Assert.Throws<InvalidOperationException>(() => listing.SubmitForModeration());
    }

    //Nộp duyệt 2 lần liên tiếp -> InvalidOperationException
    [Fact]
    public void SubmitForModerationWhenAlreadyPendingModerationShouldThrowInvalidOperationException()
    {
        var listing = CreateListing();
        listing.SubmitForModeration();
        Assert.Throws<InvalidOperationException>(() => listing.SubmitForModeration());
    }

    //Ẩn tin khi đang ở trạng thái Draft -> InvalidOperationException
    [Fact]
    public void HideFromDraftShouldThrowInvalidOperationException()
    {
        var listing = CreateListing();
        Assert.Throws<InvalidOperationException>(() => listing.Hide());
    }

    //Ẩn tin khi đang chờ duyệt -> InvalidOperationException
    [Fact]
    public void HideFromPendingModerationShouldThrowInvalidOperationException()
    {
        var listing = CreatePendingListing();
        Assert.Throws<InvalidOperationException>(() => listing.Hide());
    }

    //Từ chối duyệt tin khi đang ở trạng thái Draft -> InvalidOperationException
    [Fact]
    public void RejectFromDraftShouldThrowInvalidOperationException()
    {
        var listing = CreateListing();
        Assert.Throws<InvalidOperationException>(() => listing.Reject());
    }

    //Tạo tin đăng đang chờ duyệt
    private static Listing CreatePendingListing()
    {
        var listing = CreateListing();
        listing.SubmitForModeration();
        return listing;
    }

    //Tạo tin đăng đã được duyệt
    private static Listing CreatePublishedListing()
    {
        var listing = CreatePendingListing();
        listing.Approve(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7));
        return listing;
    }

    //Tạo tin đăng đã bị từ chối
    private static Listing CreateRejectedListing()
    {
        var listing = CreatePendingListing();
        listing.Reject();
        return listing;
    }

    //Tạo tin đăng đã bị ẩn đi
    private static Listing CreateHiddenListing()
    {
        var listing = CreatePublishedListing();
        listing.Hide();
        return listing;
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
