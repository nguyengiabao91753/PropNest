using PropNest.Domain.Common;

namespace PropNest.Domain.Listings;

public sealed class Listing : AuditableEntity
{
    private static readonly HashSet<string> ValidAdministrativeModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "PostMerger",
        "PreMerger"
    };

    private Listing()
    {
    }

    public Listing(
        long ownerUserId,
        string title,
        string? description,
        string propertyType,
        string listingType,
        decimal price,
        decimal area,
        string city,
        string? district,
        string? ward,
        string address,
        int? floors = null,
        int? bedrooms = null,
        int? bathrooms = null,
        string? balconyDirection = null,
        string administrativeMode = "PostMerger",
        int? provinceCode = null,
        int? districtCode = null,
        int? wardCode = null,
        string? legacyAddress = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ownerUserId);

        OwnerUserId = ownerUserId;
        ApplyContent(
            title,
            description,
            propertyType,
            listingType,
            price,
            area,
            city,
            district,
            ward,
            address,
            floors,
            bedrooms,
            bathrooms,
            balconyDirection,
            administrativeMode,
            provinceCode,
            districtCode,
            wardCode,
            legacyAddress);
        Status = ListingStatus.Draft;
        ModerationDecision = ModerationDecision.NotRequested;
    }

    public long ListingId { get; private set; }

    public long OwnerUserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string PropertyType { get; private set; } = string.Empty;

    public string ListingType { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public decimal Area { get; private set; }

    public int? Floors { get; private set; }

    public int? Bedrooms { get; private set; }

    public int? Bathrooms { get; private set; }

    public string? BalconyDirection { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string District { get; private set; } = string.Empty;

    public string? Ward { get; private set; }

    public string Address { get; private set; } = string.Empty;

    public string AdministrativeMode { get; private set; } = "PostMerger";

    public int? ProvinceCode { get; private set; }

    public int? DistrictCode { get; private set; }

    public int? WardCode { get; private set; }

    public string? LegacyAddress { get; private set; }

    public ListingStatus Status { get; private set; }

    public ModerationDecision ModerationDecision { get; private set; }

    public string PackageCode { get; private set; } = ListingPackagePolicy.StandardPackageCode; // 'Standard' | 'VIP'

    public DateTimeOffset? StartDate { get; private set; }

    public DateTimeOffset? EndDate { get; private set; }

    public DateTimeOffset? PackageEndDate { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public ICollection<ListingHistory> Histories { get; private set; } = new List<ListingHistory>();
    public ICollection<ListingImage> Images { get; private set; } = new List<ListingImage>();

    public void UpdateContent(
        string title,
        string? description,
        string propertyType,
        string listingType,
        decimal price,
        decimal area,
        string city,
        string? district,
        string? ward,
        string address,
        int? floors = null,
        int? bedrooms = null,
        int? bathrooms = null,
        string? balconyDirection = null,
        string administrativeMode = "PostMerger",
        int? provinceCode = null,
        int? districtCode = null,
        int? wardCode = null,
        string? legacyAddress = null)
    {
        EnsureNotDeleted();
        ApplyContent(
            title,
            description,
            propertyType,
            listingType,
            price,
            area,
            city,
            district,
            ward,
            address,
            floors,
            bedrooms,
            bathrooms,
            balconyDirection,
            administrativeMode,
            provinceCode,
            districtCode,
            wardCode,
            legacyAddress);

        if (Status is ListingStatus.Published or ListingStatus.Hidden or ListingStatus.PendingPayment or ListingStatus.Expired)
        {
            Status = ListingStatus.PendingModeration;
            ModerationDecision = ModerationDecision.Pending;
        }

        Touch();
    }

    public void SubmitForModeration()
    {
        EnsureNotDeleted();
        if (Status is not (ListingStatus.Draft or ListingStatus.Rejected or ListingStatus.Hidden))
        {
            throw new InvalidOperationException($"Listing in {Status} cannot be submitted.");
        }

        Status = ListingStatus.PendingModeration;
        ModerationDecision = ModerationDecision.Pending;
        Touch();
    }

    public void Approve(DateTimeOffset? utcNow = null)
    {
        if (Status != ListingStatus.PendingModeration)
        {
            throw new InvalidOperationException("Only a listing pending moderation can be approved.");
        }

        var now = utcNow ?? DateTimeOffset.UtcNow;
        ModerationDecision = ModerationDecision.Approved;

        // If edited while an already-paid window is still valid, restore Published without charging again.
        if (EndDate.HasValue && EndDate.Value > now)
        {
            if (!string.Equals(PackageCode, ListingPackagePolicy.StandardPackageCode, StringComparison.OrdinalIgnoreCase)
                && PackageEndDate.HasValue
                && PackageEndDate.Value <= now)
            {
                PackageCode = ListingPackagePolicy.StandardPackageCode;
                PackageEndDate = null;
            }

            Status = ListingStatus.Published;
        }
        else
        {
            PackageCode = ListingPackagePolicy.StandardPackageCode;
            StartDate = null;
            EndDate = null;
            PackageEndDate = null;
            Status = ListingStatus.PendingPayment;
        }

        Touch();
    }

    public void Reject()
    {
        if (Status != ListingStatus.PendingModeration)
        {
            throw new InvalidOperationException("Only a listing pending moderation can be rejected.");
        }

        Status = ListingStatus.Rejected;
        ModerationDecision = ModerationDecision.Rejected;
        Touch();
    }

    public void MarkForManualReview()
    {
        if (Status != ListingStatus.PendingModeration)
        {
            throw new InvalidOperationException("Only a listing pending moderation can be marked for manual review.");
        }

        ModerationDecision = ModerationDecision.ManualReview;
        Touch();
    }

    public void Hide()
    {
        if (Status != ListingStatus.Published)
        {
            throw new InvalidOperationException("Only a published listing can be hidden.");
        }

        Status = ListingStatus.Hidden;
        Touch();
    }

    public void Unhide(ListingStatus targetStatus, DateTimeOffset? now = null)
    {
        EnsureNotDeleted();
        if (Status != ListingStatus.Hidden)
        {
            throw new InvalidOperationException("Only a hidden listing can be restored.");
        }

        if (targetStatus is ListingStatus.Hidden or ListingStatus.Deleted)
        {
            throw new ArgumentException($"Cannot restore a hidden listing to {targetStatus}.", nameof(targetStatus));
        }

        var currentTime = now ?? DateTimeOffset.UtcNow;
        if (targetStatus == ListingStatus.Published && EndDate.HasValue && EndDate.Value <= currentTime)
        {
            targetStatus = ListingStatus.Expired;
        }

        if (targetStatus == ListingStatus.Published
            && !string.Equals(PackageCode, ListingPackagePolicy.StandardPackageCode, StringComparison.OrdinalIgnoreCase)
            && PackageEndDate.HasValue
            && PackageEndDate.Value <= currentTime)
        {
            PackageCode = ListingPackagePolicy.StandardPackageCode;
            PackageEndDate = null;
        }

        if (targetStatus == ListingStatus.Expired)
        {
            PackageCode = ListingPackagePolicy.StandardPackageCode;
            PackageEndDate = null;
        }

        Status = targetStatus;
        ModerationDecision = targetStatus switch
        {
            ListingStatus.Published or ListingStatus.Expired => ModerationDecision.Approved,
            ListingStatus.PendingModeration => ModerationDecision.Pending,
            ListingStatus.Rejected => ModerationDecision.Rejected,
            _ => ModerationDecision
        };
        Touch();
    }

    public void Delete()
    {
        EnsureNotDeleted();
        if (Status != ListingStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft listing can be deleted.");
        }
        Status = ListingStatus.Deleted;
        Touch(); // Update UpdatedAt
    }

    public void Expire() => Expire(DateTimeOffset.UtcNow);

    public void Expire(DateTimeOffset now)
    {
        if (Status == ListingStatus.Published && EndDate <= now)
        {
            Status = ListingStatus.Expired;
            PackageCode = ListingPackagePolicy.StandardPackageCode;
            PackageEndDate = null;
            Touch();
        }
    }

    public bool DowngradePackageIfExpired(DateTimeOffset now)
    {
        if (Status == ListingStatus.Published
            && !string.Equals(PackageCode, ListingPackagePolicy.StandardPackageCode, StringComparison.OrdinalIgnoreCase)
            && PackageEndDate.HasValue
            && PackageEndDate.Value <= now
            && EndDate.HasValue
            && EndDate.Value > now)
        {
            PackageCode = ListingPackagePolicy.StandardPackageCode;
            PackageEndDate = null;
            Touch();
            return true;
        }

        return false;
    }

    public void ApplyPackagePurchase(string packageKind, int durationDays, DateTimeOffset? utcNow = null)
    {
        EnsureNotDeleted();
        if (!ListingPackagePolicy.CanPurchasePackage(Status, ModerationDecision))
        {
            throw new InvalidOperationException($"Cannot purchase a package for listing in status {Status} ({ModerationDecision}).");
        }

        if (!ListingPackagePolicy.IsSupportedTierKind(packageKind))
        {
            throw new ArgumentException($"Unsupported package tier '{packageKind}'.", nameof(packageKind));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationDays);

        var now = utcNow ?? DateTimeOffset.UtcNow;
        var hasActiveLifespan = Status == ListingStatus.Published && EndDate.HasValue && EndDate.Value > now;
        var isVipTier = string.Equals(packageKind.Trim(), ListingPackagePolicy.VipPackageCode, StringComparison.OrdinalIgnoreCase);

        if (isVipTier)
        {
            var newVipEnd = ListingPackagePolicy.HasActiveVipPackage(PackageCode, PackageEndDate, now)
                ? ListingPackagePolicy.ExtendEndDate(PackageEndDate!.Value, durationDays)
                : ListingPackagePolicy.ComputeNewEndDate(now, durationDays);

            PackageCode = ListingPackagePolicy.VipPackageCode;
            PackageEndDate = newVipEnd;
            StartDate ??= now;
            EndDate = hasActiveLifespan && EndDate!.Value > newVipEnd ? EndDate.Value : newVipEnd;
        }
        else
        {
            if (hasActiveLifespan)
            {
                EndDate = ListingPackagePolicy.ExtendEndDate(EndDate!.Value, durationDays);
                StartDate ??= now;
                // Preserve active VIP tier/PackageEndDate if seller extends base lifespan while VIP is active.
            }
            else
            {
                PackageCode = ListingPackagePolicy.StandardPackageCode;
                PackageEndDate = null;
                StartDate = now;
                EndDate = ListingPackagePolicy.ComputeNewEndDate(now, durationDays);
            }
        }

        Status = ListingStatus.Published;
        Touch();
    }

    private void ApplyContent(
        string title,
        string? description,
        string propertyType,
        string listingType,
        decimal price,
        decimal area,
        string city,
        string? district,
        string? ward,
        string address,
        int? floors,
        int? bedrooms,
        int? bathrooms,
        string? balconyDirection,
        string administrativeMode,
        int? provinceCode,
        int? districtCode,
        int? wardCode,
        string? legacyAddress)
    {
        var normalizedMode = string.IsNullOrWhiteSpace(administrativeMode)
            ? "PostMerger"
            : administrativeMode.Trim();

        if (!ValidAdministrativeModes.Contains(normalizedMode))
        {
            throw new ArgumentException("AdministrativeMode must be 'PostMerger' or 'PreMerger'.", nameof(administrativeMode));
        }

        normalizedMode = string.Equals(normalizedMode, "PreMerger", StringComparison.OrdinalIgnoreCase)
            ? "PreMerger"
            : "PostMerger";

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(propertyType) ||
            string.IsNullOrWhiteSpace(listingType) || string.IsNullOrWhiteSpace(city) ||
            string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Required listing content is missing.");
        }

        if (normalizedMode == "PreMerger" && string.IsNullOrWhiteSpace(district))
        {
            throw new ArgumentException("District is required for pre-merger addresses.", nameof(district));
        }

        if (price < 0 || area <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be non-negative and area must be positive.");
        }

        if (floors is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(floors), "Floors must be at least 1 when specified.");
        }

        if (bedrooms is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bedrooms), "Bedrooms cannot be negative.");
        }

        if (bathrooms is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bathrooms), "Bathrooms cannot be negative.");
        }

        Title = title.Trim();
        Description = description?.Trim();
        PropertyType = propertyType.Trim();
        ListingType = listingType.Trim();
        Price = price;
        Area = area;
        Floors = floors;
        Bedrooms = bedrooms;
        Bathrooms = bathrooms;
        BalconyDirection = string.IsNullOrWhiteSpace(balconyDirection) ? null : balconyDirection.Trim();
        City = city.Trim();
        District = district?.Trim() ?? string.Empty;
        Ward = string.IsNullOrWhiteSpace(ward) ? null : ward.Trim();
        Address = address.Trim();
        AdministrativeMode = normalizedMode;
        ProvinceCode = provinceCode;
        DistrictCode = districtCode;
        WardCode = wardCode;
        LegacyAddress = string.IsNullOrWhiteSpace(legacyAddress) ? null : legacyAddress.Trim();
    }

    private void EnsureNotDeleted()
    {
        if (Status == ListingStatus.Deleted)
        {
            throw new InvalidOperationException("A deleted listing cannot be changed.");
        }
    }
}
