using PropNest.Application.Common;
using PropNest.Domain.Common;
using PropNest.Domain.Listings;

namespace PropNest.Application.Listings.Search;

/// <summary>
/// Schema document đại diện cho tin đăng lưu trên Meilisearch.
/// Chỉ chứa tin đủ điều kiện hiển thị: Status == Published && ModerationDecision == Approved && EndDate > now.
/// </summary>
public sealed class ListingSearchDocument
{
    public long ListingId { get; set; }
    public long OwnerUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleNormalized { get; set; } = string.Empty;
    public string DescriptionPlainText { get; set; } = string.Empty;
    public string DescriptionNormalizedText { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public string ListingType { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Area { get; set; }
    public int? Floors { get; set; }
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public string? BalconyDirection { get; set; }
    public string AdministrativeMode { get; set; } = "PostMerger";
    public int? ProvinceCode { get; set; }
    public int? DistrictCode { get; set; }
    public int? WardCode { get; set; }
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string? Ward { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? LegacyAddress { get; set; }
    public IReadOnlyList<string> LocationTokens { get; set; } = Array.Empty<string>();
    public string SearchNormalizedText { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ModerationDecision { get; set; } = string.Empty;
    public string PackageCode { get; set; } = string.Empty;
    public bool IsVip { get; set; }
    public int PackageTierWeight { get; set; }
    public long? StartDateUnix { get; set; }
    public long? PackageEndDateUnix { get; set; }
    public long? EndDateUnix { get; set; }
    public long? LastBumpedAtUnix { get; set; }
    public long RankTimestampUnix { get; set; }
    public long CreatedAtUnix { get; set; }
    public long? UpdatedAtUnix { get; set; }

    /// <summary>
    /// Ánh xạ từ Domain Entity Listing sang Meilisearch Document.
    /// </summary>
    public static ListingSearchDocument FromEntity(
        Listing listing,
        string? coverImageUrl = null,
        DateTimeOffset? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(listing);

        var now = utcNow ?? DateTimeOffset.UtcNow;
        var hasVipCode = string.Equals(listing.PackageCode, ListingPackagePolicy.VipPackageCode, StringComparison.OrdinalIgnoreCase);
        var isVipExpired = hasVipCode && listing.PackageEndDate.HasValue && listing.PackageEndDate.Value <= now;
        var isVip = hasVipCode && !isVipExpired;
        var effectivePackageCode = isVipExpired ? ListingPackagePolicy.StandardPackageCode : listing.PackageCode;
        var effectivePackageEndDate = isVipExpired ? null : listing.PackageEndDate;
        var tierWeight = isVip ? 200 : 100;

        var cover = coverImageUrl;
        if (string.IsNullOrWhiteSpace(cover) && listing.Images.Count > 0)
        {
            cover = listing.Images
                .Where(i => string.Equals(i.Status, "Active", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(i => i.IsCover)
                .ThenBy(i => i.SortOrder)
                .Select(i => i.Url)
                .FirstOrDefault();
        }

        var descriptionPlain = RichTextHtmlHelper.ToPlainTextPreview(listing.Description);
        var titleNormalized = VietnameseSearchNormalizer.Normalize(listing.Title);
        var descriptionNormalized = VietnameseSearchNormalizer.Normalize(descriptionPlain);
        var searchNormalized = VietnameseSearchNormalizer.BuildSearchNormalizedText(
            listing.Title,
            listing.City,
            listing.District,
            listing.Ward,
            listing.Address,
            listing.LegacyAddress);

        var locationTokens = VietnameseSearchNormalizer.BuildLocationFilterTokens(
            listing.City,
            listing.District,
            listing.Ward,
            listing.LegacyAddress);

        var rankTimestamp = (listing.StartDate ?? listing.CreatedAt).ToUnixTimeSeconds();

        return new ListingSearchDocument
        {
            ListingId = listing.ListingId,
            OwnerUserId = listing.OwnerUserId,
            Title = listing.Title,
            TitleNormalized = titleNormalized,
            DescriptionPlainText = descriptionPlain,
            DescriptionNormalizedText = descriptionNormalized,
            PropertyType = listing.PropertyType,
            ListingType = listing.ListingType,
            Price = listing.Price,
            Area = listing.Area,
            Floors = listing.Floors,
            Bedrooms = listing.Bedrooms,
            Bathrooms = listing.Bathrooms,
            BalconyDirection = listing.BalconyDirection,
            AdministrativeMode = listing.AdministrativeMode,
            ProvinceCode = listing.ProvinceCode,
            DistrictCode = listing.DistrictCode,
            WardCode = listing.WardCode,
            City = listing.City,
            District = listing.District,
            Ward = listing.Ward,
            Address = listing.Address,
            LegacyAddress = listing.LegacyAddress,
            LocationTokens = locationTokens,
            SearchNormalizedText = searchNormalized,
            ThumbnailUrl = cover,
            Status = listing.Status.ToString(),
            ModerationDecision = listing.ModerationDecision.ToString(),
            PackageCode = effectivePackageCode,
            IsVip = isVip,
            PackageTierWeight = tierWeight,
            StartDateUnix = listing.StartDate?.ToUnixTimeSeconds(),
            PackageEndDateUnix = effectivePackageEndDate?.ToUnixTimeSeconds(),
            EndDateUnix = listing.EndDate?.ToUnixTimeSeconds(),
            LastBumpedAtUnix = null,
            RankTimestampUnix = rankTimestamp,
            CreatedAtUnix = listing.CreatedAt.ToUnixTimeSeconds(),
            UpdatedAtUnix = listing.UpdatedAt.ToUnixTimeSeconds()
        };
    }
}
