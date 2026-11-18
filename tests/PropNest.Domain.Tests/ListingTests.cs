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

        Assert.Throws<InvalidOperationException>(() => listing.Approve(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void SubmitForModerationFromRejectedShouldMovesToPendingModeration()
    {
        var listing = CreateRejectedListing();
        listing.SubmitForModeration();
        Assert.Equal(ListingStatus.PendingModeration, listing.Status);
        Assert.Equal(ModerationDecision.Pending, listing.ModerationDecision);
    }

    [Fact]
    public void SubmitForModerationFromHiddenShouldMoveToPendingModeration()
    {
        var listing = CreateHiddenListing();
        listing.SubmitForModeration();
        Assert.Equal(ListingStatus.PendingModeration, listing.Status);
        Assert.Equal(ModerationDecision.Pending, listing.ModerationDecision);
    }

    [Fact]
    public void ApproveFromPendingModerationWithoutActivePaidPeriodMovesToPendingPayment()
    {
        var listing = CreatePendingListing();

        listing.Approve(DateTimeOffset.UtcNow);

        Assert.Equal(ListingStatus.PendingPayment, listing.Status);
        Assert.Equal(ModerationDecision.Approved, listing.ModerationDecision);
        Assert.Null(listing.StartDate);
        Assert.Null(listing.EndDate);
        Assert.Null(listing.PackageEndDate);
    }

    [Fact]
    public void ApproveEditedPublishedListingWithinPaidWindowRestoresPublishedWithoutRepayment()
    {
        var nov18 = new DateTimeOffset(2026, 11, 18, 9, 0, 0, ListingPackagePolicy.VietnamUtcOffset);
        var listing = CreatePendingListing();
        listing.Approve(nov18);
        listing.ApplyPackagePurchase("Standard", 15, nov18);
        var paidEndDate = listing.EndDate;

        listing.UpdateContent(
            "Updated apartment title",
            "Updated description content.",
            "Apartment",
            "Sale",
            4_600_000_000m,
            75m,
            "Ho Chi Minh City",
            "District 7",
            "Tan Phu",
            "1 Nguyen Van Linh");
        Assert.Equal(ListingStatus.PendingModeration, listing.Status);

        listing.Approve(nov18.AddDays(2));

        Assert.Equal(ListingStatus.Published, listing.Status);
        Assert.Equal(ModerationDecision.Approved, listing.ModerationDecision);
        Assert.Equal(paidEndDate, listing.EndDate);
    }

    [Theory]
    [InlineData(ListingStatus.PendingPayment)]
    [InlineData(ListingStatus.Expired)]
    public void UpdateContentFromPendingPaymentOrExpiredRequiresReModeration(ListingStatus initialStatus)
    {
        var nov1 = new DateTimeOffset(2026, 11, 1, 9, 0, 0, ListingPackagePolicy.VietnamUtcOffset);
        var listing = CreatePendingListing();
        listing.Approve(nov1);

        if (initialStatus == ListingStatus.Expired)
        {
            listing.ApplyPackagePurchase("Standard", 7, nov1);
            listing.Expire(nov1.AddDays(10));
        }

        Assert.Equal(initialStatus, listing.Status);

        listing.UpdateContent(
            "Modified title after approval",
            "Modified description content.",
            "Apartment",
            "Sale",
            5_000_000_000m,
            80m,
            "Ho Chi Minh City",
            "District 7",
            "Tan Phu",
            "1 Nguyen Van Linh");

        Assert.Equal(ListingStatus.PendingModeration, listing.Status);
        Assert.Equal(ModerationDecision.Pending, listing.ModerationDecision);
    }

    [Fact]
    public void RejectFromPendingModerationShouldMoveToRejected()
    {
        var listing = CreatePendingListing();
        listing.Reject();
        Assert.Equal(ListingStatus.Rejected, listing.Status);
        Assert.Equal(ModerationDecision.Rejected, listing.ModerationDecision);
    }

    [Fact]
    public void HideFromPublishedShouldMoveToHidden()
    {
        var listing = CreatePublishedListing();
        listing.Hide();
        Assert.Equal(ListingStatus.Hidden, listing.Status);
    }

    [Fact]
    public void SubmitForModerationFromPublishedShouldThrowInvalidOperationException()
    {
        var listing = CreatePublishedListing();
        Assert.Throws<InvalidOperationException>(() => listing.SubmitForModeration());
    }

    [Fact]
    public void SubmitForModerationWhenAlreadyPendingModerationShouldThrowInvalidOperationException()
    {
        var listing = CreateListing();
        listing.SubmitForModeration();
        Assert.Throws<InvalidOperationException>(() => listing.SubmitForModeration());
    }

    [Fact]
    public void HideFromDraftShouldThrowInvalidOperationException()
    {
        var listing = CreateListing();
        Assert.Throws<InvalidOperationException>(() => listing.Hide());
    }

    [Fact]
    public void HideFromPendingModerationShouldThrowInvalidOperationException()
    {
        var listing = CreatePendingListing();
        Assert.Throws<InvalidOperationException>(() => listing.Hide());
    }

    [Fact]
    public void RejectFromDraftShouldThrowInvalidOperationException()
    {
        var listing = CreateListing();
        Assert.Throws<InvalidOperationException>(() => listing.Reject());
    }

    [Fact]
    public void ExpireFromPublishedWithPastEndDateShouldMoveToExpired()
    {
        var listing = CreatePendingListing();
        var past = DateTimeOffset.UtcNow.AddDays(-20);
        listing.Approve(past);
        listing.ApplyPackagePurchase("Standard", 7, past);

        listing.Expire(DateTimeOffset.UtcNow);

        Assert.Equal(ListingStatus.Expired, listing.Status);
        Assert.Equal("Standard", listing.PackageCode);
        Assert.Null(listing.PackageEndDate);
    }

    [Fact]
    public void ExpireParameterlessShouldMoveToExpiredWhenEndDatePast()
    {
        var listing = CreatePendingListing();
        var past = DateTimeOffset.UtcNow.AddDays(-20);
        listing.Approve(past);
        listing.ApplyPackagePurchase("Standard", 7, past);

        listing.Expire();

        Assert.Equal(ListingStatus.Expired, listing.Status);
    }

    [Fact]
    public void ExpireFromPublishedWithFutureEndDateShouldRemainPublished()
    {
        var listing = CreatePublishedListing();

        listing.Expire(DateTimeOffset.UtcNow);

        Assert.Equal(ListingStatus.Published, listing.Status);
    }

    [Fact]
    public void ExpireFromDraftShouldRemainDraft()
    {
        var listing = CreateListing();

        listing.Expire();

        Assert.Equal(ListingStatus.Draft, listing.Status);
    }

    [Fact]
    public void ApplyPackagePurchaseFromDraftOrPendingModerationShouldThrowInvalidOperationException()
    {
        var draft = CreateListing();
        Assert.Throws<InvalidOperationException>(() => draft.ApplyPackagePurchase("Standard", 15, DateTimeOffset.UtcNow));

        var pending = CreatePendingListing();
        Assert.Throws<InvalidOperationException>(() => pending.ApplyPackagePurchase("VIP", 7, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void BuyingVip7WhenStandardHasLongerEndDatePreservesStandardEndDateAndDowngradesLater()
    {
        // Standard 15d starting 12/11 -> expires 26/11 23:59:59 +07:00
        var nov12 = new DateTimeOffset(2026, 11, 12, 10, 0, 0, ListingPackagePolicy.VietnamUtcOffset);
        var nov18 = new DateTimeOffset(2026, 11, 18, 9, 0, 0, ListingPackagePolicy.VietnamUtcOffset);
        var nov25 = new DateTimeOffset(2026, 11, 25, 0, 5, 0, ListingPackagePolicy.VietnamUtcOffset);

        var listing = CreatePendingListing();
        listing.Approve(nov12);
        listing.ApplyPackagePurchase("Standard", 15, nov12);
        Assert.Equal(new DateTimeOffset(2026, 11, 26, 23, 59, 59, ListingPackagePolicy.VietnamUtcOffset), listing.EndDate);

        // Buy VIP 7d on 18/11 -> VIP until 24/11 23:59:59, listing still lives until 26/11 23:59:59
        listing.ApplyPackagePurchase("VIP", 7, nov18);
        Assert.Equal(ListingStatus.Published, listing.Status);
        Assert.Equal("VIP", listing.PackageCode);
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 23, 59, 59, ListingPackagePolicy.VietnamUtcOffset), listing.PackageEndDate);
        Assert.Equal(new DateTimeOffset(2026, 11, 26, 23, 59, 59, ListingPackagePolicy.VietnamUtcOffset), listing.EndDate);

        // On 25/11: VIP ended, listing downgrades to Standard until 26/11
        var downgraded = listing.DowngradePackageIfExpired(nov25);
        Assert.True(downgraded);
        Assert.Equal(ListingStatus.Published, listing.Status);
        Assert.Equal("Standard", listing.PackageCode);
        Assert.Null(listing.PackageEndDate);
        Assert.Equal(new DateTimeOffset(2026, 11, 26, 23, 59, 59, ListingPackagePolicy.VietnamUtcOffset), listing.EndDate);

        // On 27/11: Standard lifespan expires
        listing.Expire(new DateTimeOffset(2026, 11, 27, 0, 1, 0, ListingPackagePolicy.VietnamUtcOffset));
        Assert.Equal(ListingStatus.Expired, listing.Status);
    }

    [Fact]
    public void BuyingVip30WhenStandardRemainingIsShorterExtendsListingEndDateToVipEndDate()
    {
        var nov12 = new DateTimeOffset(2026, 11, 12, 10, 0, 0, ListingPackagePolicy.VietnamUtcOffset);
        var nov18 = new DateTimeOffset(2026, 11, 18, 9, 0, 0, ListingPackagePolicy.VietnamUtcOffset);

        var listing = CreatePendingListing();
        listing.Approve(nov12);
        listing.ApplyPackagePurchase("Standard", 15, nov12); // until 26/11

        listing.ApplyPackagePurchase("VIP", 30, nov18); // 18/11 + 29d = 17/12 23:59:59 +07:00
        var expectedVip30End = new DateTimeOffset(2026, 12, 17, 23, 59, 59, ListingPackagePolicy.VietnamUtcOffset);

        Assert.Equal(ListingStatus.Published, listing.Status);
        Assert.Equal("VIP", listing.PackageCode);
        Assert.Equal(expectedVip30End, listing.PackageEndDate);
        Assert.Equal(expectedVip30End, listing.EndDate);
    }

    [Fact]
    public void BuyingStandardWhileVipIsActiveExtendsLifespanAndPreservesVipWindow()
    {
        var nov18 = new DateTimeOffset(2026, 11, 18, 9, 0, 0, ListingPackagePolicy.VietnamUtcOffset);
        var listing = CreatePendingListing();
        listing.Approve(nov18);
        listing.ApplyPackagePurchase("VIP", 7, nov18); // VIP & EndDate = 24/11 23:59:59

        listing.ApplyPackagePurchase("Standard", 15, nov18); // Extend EndDate by 15d -> 09/12 23:59:59

        Assert.Equal("VIP", listing.PackageCode);
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 23, 59, 59, ListingPackagePolicy.VietnamUtcOffset), listing.PackageEndDate);
        Assert.Equal(new DateTimeOffset(2026, 12, 9, 23, 59, 59, ListingPackagePolicy.VietnamUtcOffset), listing.EndDate);
    }

    [Fact]
    public void BuyingVipWhileVipIsActiveStacksBothVipAndListingEndDate()
    {
        var nov18 = new DateTimeOffset(2026, 11, 18, 9, 0, 0, ListingPackagePolicy.VietnamUtcOffset);
        var listing = CreatePendingListing();
        listing.Approve(nov18);
        listing.ApplyPackagePurchase("VIP", 7, nov18); // 24/11

        listing.ApplyPackagePurchase("VIP", 7, nov18); // +7d -> 01/12

        var expectedDec01 = new DateTimeOffset(2026, 12, 1, 23, 59, 59, ListingPackagePolicy.VietnamUtcOffset);
        Assert.Equal("VIP", listing.PackageCode);
        Assert.Equal(expectedDec01, listing.PackageEndDate);
        Assert.Equal(expectedDec01, listing.EndDate);
    }

    [Fact]
    public void UnhideFromHiddenShouldRestorePreviousPublishedStatus()
    {
        var listing = CreateHiddenListing();

        listing.Unhide(ListingStatus.Published, DateTimeOffset.UtcNow);

        Assert.Equal(ListingStatus.Published, listing.Status);
        Assert.Equal(ModerationDecision.Approved, listing.ModerationDecision);
    }

    [Fact]
    public void UnhideToPublishedWhenEndDatePastShouldMoveToExpired()
    {
        var past = DateTimeOffset.UtcNow.AddDays(-15);
        var listing = CreatePendingListing();
        listing.Approve(past);
        listing.ApplyPackagePurchase("Standard", 7, past);
        listing.Hide();

        listing.Unhide(ListingStatus.Published, DateTimeOffset.UtcNow);

        Assert.Equal(ListingStatus.Expired, listing.Status);
        Assert.Equal(ModerationDecision.Approved, listing.ModerationDecision);
    }

    [Fact]
    public void UnhideFromNonHiddenShouldThrowInvalidOperationException()
    {
        var listing = CreatePublishedListing();

        Assert.Throws<InvalidOperationException>(() => listing.Unhide(ListingStatus.Draft));
    }

    [Theory]
    [InlineData(ListingStatus.Hidden)]
    [InlineData(ListingStatus.Deleted)]
    public void UnhideToInvalidTargetStatusShouldThrowArgumentException(ListingStatus targetStatus)
    {
        var listing = CreateHiddenListing();

        Assert.Throws<ArgumentException>(() => listing.Unhide(targetStatus));
    }

    private static Listing CreatePendingListing()
    {
        var listing = CreateListing();
        listing.SubmitForModeration();
        return listing;
    }

    private static Listing CreatePublishedListing()
    {
        var listing = CreatePendingListing();
        var now = DateTimeOffset.UtcNow;
        listing.Approve(now);
        listing.ApplyPackagePurchase("Standard", 15, now);
        return listing;
    }

    private static Listing CreateRejectedListing()
    {
        var listing = CreatePendingListing();
        listing.Reject();
        return listing;
    }

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
