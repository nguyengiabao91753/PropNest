namespace PropNest.Domain.Listings;

public static class ListingPackagePolicy
{
    public const string StandardPackageCode = "Standard";
    public const string VipPackageCode = "VIP";

    public static readonly TimeSpan VietnamUtcOffset = TimeSpan.FromHours(7);

    public static DateTimeOffset ComputeNewEndDate(DateTimeOffset utcNow, int durationDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationDays);

        var vnNow = utcNow.ToOffset(VietnamUtcOffset);
        var vnEndDate = vnNow.Date
            .AddDays(durationDays - 1)
            .AddHours(23)
            .AddMinutes(59)
            .AddSeconds(59);

        return new DateTimeOffset(vnEndDate, VietnamUtcOffset);
    }

    public static DateTimeOffset ExtendEndDate(DateTimeOffset existingEnd, int durationDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationDays);

        var vnExisting = existingEnd.ToOffset(VietnamUtcOffset);
        var vnExtendedDate = vnExisting.Date
            .AddDays(durationDays)
            .AddHours(23)
            .AddMinutes(59)
            .AddSeconds(59);

        return new DateTimeOffset(vnExtendedDate, VietnamUtcOffset);
    }

    public static bool IsSupportedTierKind(string? kind) =>
        string.Equals(kind?.Trim(), StandardPackageCode, StringComparison.OrdinalIgnoreCase)
        || string.Equals(kind?.Trim(), VipPackageCode, StringComparison.OrdinalIgnoreCase);

    public static bool IsPurchasablePackage(string? kind, decimal price, bool isActive) =>
        isActive && price > 0m && IsSupportedTierKind(kind);

    public static bool HasActiveVipPackage(string? packageCode, DateTimeOffset? packageEndDate, DateTimeOffset utcNow) =>
        string.Equals(packageCode, VipPackageCode, StringComparison.OrdinalIgnoreCase)
        && packageEndDate.HasValue
        && packageEndDate.Value > utcNow;

    public static bool CanPurchasePackage(ListingStatus status, ModerationDecision moderationDecision) =>
        moderationDecision == ModerationDecision.Approved
        && status is ListingStatus.PendingPayment or ListingStatus.Published or ListingStatus.Expired;
}
