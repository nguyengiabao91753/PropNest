using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PropNest.Application.Common;
using PropNest.Application.Listings;
using PropNest.Domain.Listings;
using PropNest.Domain.Packages;
using PropNest.Domain.Workflows;

namespace PropNest.Infrastructure.Persistence.Repositories;

public sealed class ListingRepository(PropNestDbContext dbContext) : IListingRepository
{
    public async Task<Listing?> GetByIdAsync(long listingId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Listings
            .Include(x => x.Images.OrderBy(img => img.SortOrder))
            .SingleOrDefaultAsync(x => x.ListingId == listingId, cancellationToken);
    }

    public async Task<ListingOwnerContactDto?> GetOwnerContactAsync(long ownerUserId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == ownerUserId)
            .Select(u => new ListingOwnerContactDto(
                u.Id,
                u.FullName,
                u.PhoneNumber ?? string.Empty,
                u.Email ?? string.Empty))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Listing>> GetByOwnerUserIdAsync(long ownerUserId, CancellationToken cancellationToken = default) =>
        await dbContext.Listings
            .Include(x => x.Images.OrderBy(img => img.SortOrder))
            .AsNoTracking()
            .Where(x => x.OwnerUserId == ownerUserId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Listing>> GetPendingModerationAsync(CancellationToken cancellationToken = default)
    {
        var items = await dbContext.Listings
            .Include(x => x.Images.OrderBy(img => img.SortOrder))
            .AsNoTracking()
            .Where(x => x.Status == ListingStatus.PendingModeration
                     && x.ModerationDecision == ModerationDecision.ManualReview)
            .ToListAsync(cancellationToken);

        return items.OrderBy(x => x.CreatedAt).ThenBy(x => x.ListingId).ToList();
    }

    public async Task<IReadOnlyList<ListingDto>> GetPendingModerationWithOwnersAsync(CancellationToken cancellationToken = default)
    {
        var query = from l in dbContext.Listings.AsNoTracking()
                    where l.Status == ListingStatus.PendingModeration
                       && l.ModerationDecision == ModerationDecision.ManualReview
                    join u in dbContext.Users.AsNoTracking() on l.OwnerUserId equals u.Id into userGroup
                    from user in userGroup.DefaultIfEmpty()
                    select new ListingDto(
                        l.ListingId,
                        l.OwnerUserId,
                        l.Title,
                        l.Description,
                        l.PropertyType,
                        l.ListingType,
                        l.Price,
                        l.Area,
                        l.City,
                        l.District,
                        l.Ward,
                        l.Address,
                        l.Status,
                        l.ModerationDecision,
                        l.PackageCode,
                        l.StartDate,
                        l.EndDate,
                        l.RowVersion,
                        user != null ? (user.FullName ?? user.UserName) : null,
                        user != null ? user.Email : null,
                        user != null ? user.PhoneNumber : null,
                        l.CreatedAt,
                        l.Images.OrderBy(img => img.SortOrder).Select(img => img.Url).ToList(),
                        l.Floors,
                        l.Bedrooms,
                        l.Bathrooms,
                        l.BalconyDirection,
                        l.AdministrativeMode,
                        l.ProvinceCode,
                        l.DistrictCode,
                        l.WardCode,
                        l.LegacyAddress,
                        l.PackageEndDate,
                        null,
                        null);

        var items = await query.ToListAsync(cancellationToken);
        return items.OrderBy(x => x.CreatedAt).ThenBy(x => x.ListingId).ToList();
    }

    public async Task<IReadOnlyList<Listing>> GetExpiredPublishedListingsAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
        await dbContext.Listings
            .Where(x => x.Status == ListingStatus.Published && x.EndDate <= now)
            .OrderBy(x => x.EndDate)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Listing>> GetListingsWithExpiredPackageAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
        await dbContext.Listings
            .Where(x => x.Status == ListingStatus.Published
                     && x.PackageCode != ListingPackagePolicy.StandardPackageCode
                     && x.PackageEndDate != null
                     && x.PackageEndDate <= now
                     && x.EndDate > now)
            .OrderBy(x => x.PackageEndDate)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Listing>> GetStalePendingModerationListingsAsync(DateTimeOffset cutoffUtc, int batchSize, CancellationToken cancellationToken = default)
    {
        var pending = await dbContext.Listings
            .Where(x => x.Status == ListingStatus.PendingModeration
                     && x.ModerationDecision == ModerationDecision.Pending)
            .OrderBy(x => x.ListingId)
            .Take(batchSize * 3)
            .ToListAsync(cancellationToken);

        return pending
            .Where(x => x.UpdatedAt <= cutoffUtc)
            .Take(batchSize)
            .ToList();
    }

    public Task AddAsync(Listing listing, CancellationToken cancellationToken = default) =>
        dbContext.Listings.AddAsync(listing, cancellationToken).AsTask();

    public void AddHistory(ListingHistory history) => dbContext.ListingHistories.Add(history);

    public Task<PackageDefinitions?> GetPackageByCodeAsync(string packageCode, CancellationToken cancellationToken = default) =>
        dbContext.PackageDefinitions.SingleOrDefaultAsync(x => x.PackageCode == packageCode && x.IsActive, cancellationToken);

    public async Task<IReadOnlyList<PackageDefinitions>> GetActivePackagesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.PackageDefinitions
            .AsNoTracking()
            .Where(x => x.IsActive && x.Price > 0m)
            .OrderBy(x => x.DurationDays)
            .ToListAsync(cancellationToken);

    public void AddModerationReview(ListingModerationReview review) => dbContext.ListingModerationReviews.Add(review);

    public void AddOutboxMessage(OutboxMessage message) => dbContext.OutboxMessages.Add(message);

    public void SetOriginalRowVersion(Listing listing, byte[] rowVersion)
    {
        if (rowVersion.Length == 0)
        {
            throw new ArgumentException("A RowVersion is required for updates.", nameof(rowVersion));
        }

        dbContext.Entry(listing).Property(x => x.RowVersion).OriginalValue = rowVersion;
    }

    public async Task<PagedResult<ListingSummaryDto>> GetPublicListingsAsync(GetPublicListingsQuery query, CancellationToken cancellationToken = default)
    {
        var targetType = query.ListingType ?? query.Type;
        if (!string.IsNullOrWhiteSpace(targetType))
        {
            targetType = targetType.Trim().ToLowerInvariant() switch
            {
                "rent" => "Rent",
                "sale" => "Sale",
                _ => targetType
            };
        }

        var now = DateTimeOffset.UtcNow;
        var queryable = dbContext.Listings.AsNoTracking()
            .Where(x => x.Status == ListingStatus.Published && (x.EndDate == null || x.EndDate > now))
            .Where(x => string.IsNullOrWhiteSpace(query.Q) ||
                        x.Title.Contains(query.Q) ||
                        x.Address.Contains(query.Q) ||
                        (x.LegacyAddress != null && x.LegacyAddress.Contains(query.Q)))
            .Where(x => query.District == null ||
                        x.District == query.District ||
                        (x.LegacyAddress != null && x.LegacyAddress.Contains(query.District)))
            .Where(x => query.City == null ||
                        x.City == query.City ||
                        (x.LegacyAddress != null && x.LegacyAddress.Contains(query.City)))
            .Where(x => query.Ward == null ||
                        x.Ward == query.Ward ||
                        (x.LegacyAddress != null && x.LegacyAddress.Contains(query.Ward)))
            .Where(x => query.PropertyType == null || x.PropertyType == query.PropertyType)
            .Where(x => targetType == null || x.ListingType == targetType)
            .Where(x => query.MinPrice == null || x.Price >= query.MinPrice)
            .Where(x => query.MaxPrice == null || x.Price <= query.MaxPrice);

        var totalCount = await queryable.CountAsync(cancellationToken);

        var items = await queryable
            .OrderBy(x => x.PackageCode == ListingPackagePolicy.VipPackageCode ? 0 : 1)
            .ThenByDescending(x => x.StartDate ?? x.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new ListingSummaryDto(
                x.ListingId,
                x.Title,
                x.Price,
                x.Area,
                x.ListingType,
                x.PropertyType,
                x.City,
                x.District,
                x.Ward,
                x.PackageCode,
                x.Address,
                x.Status,
                x.CreatedAt,
                x.ModerationDecision,
                x.Floors,
                x.Bedrooms,
                x.Bathrooms,
                x.BalconyDirection,
                x.AdministrativeMode,
                x.LegacyAddress,
                x.Images
                    .OrderBy(i => i.IsCover ? 0 : 1)
                    .ThenBy(i => i.SortOrder)
                    .Select(i => i.Url)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new PagedResult<ListingSummaryDto>(items, totalCount, query.PageNumber, query.PageSize);
    }

    public async Task<IReadOnlyList<ListingHistoryDto>> GetHistoriesByListingIdAsync(long listingId, CancellationToken cancellationToken = default)
    {
        var query = from history in dbContext.ListingHistories.AsNoTracking()
                    where history.ListingId == listingId
                    join user in dbContext.Users.AsNoTracking() on history.ActorUserId equals user.Id into userGroup
                    from actor in userGroup.DefaultIfEmpty()
                    orderby history.HistoryId ascending
                    select new ListingHistoryDto(
                        history.ActionType,
                        history.ActionDate,
                        history.ActorUserId,
                        actor != null ? (actor.FullName ?? actor.UserName) : "Hệ thống",
                        actor != null ? actor.Role : "System",
                        history.SnapshotJson,
                        history.DeltasJson,
                        history.Note);
        var items = await query.ToListAsync(cancellationToken);
        return items.OrderBy(x => x.ActionDate).ToList();
    }

    public Task<Listing?> GetByIdWithImagesAsync(long listingId, CancellationToken cancellationToken = default) =>
        dbContext.Listings
            .Include(x => x.Images)
            .SingleOrDefaultAsync(x => x.ListingId == listingId, cancellationToken);

    public async Task<IReadOnlyList<ListingImage>> GetImagesByListingIdAsync(long listingId, CancellationToken cancellationToken = default) =>
        await dbContext.ListingImages
            .AsNoTracking()
            .Where(x => x.ListingId == listingId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ListingImage>> GetTrackedImagesByListingIdAsync(long listingId, CancellationToken cancellationToken = default) =>
        await dbContext.ListingImages
            .Where(x => x.ListingId == listingId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

    public Task<ListingImage?> GetImageByIdAsync(long imageId, CancellationToken cancellationToken = default) =>
        dbContext.ListingImages
            .SingleOrDefaultAsync(x => x.ImageId == imageId, cancellationToken);

    public void AddImage(ListingImage image) => dbContext.ListingImages.Add(image);

    public void RemoveImage(ListingImage image) => dbContext.ListingImages.Remove(image);

    public async Task<AmeModerationResult?> GetLatestAmeResultAsync(
        long listingId, CancellationToken cancellationToken = default)
    {
        return await dbContext.AmeModerationResults
            .AsNoTracking()
            .Where(r => r.ListingId == listingId)
            .OrderByDescending(r => r.ResultId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string?> GetLatestRejectReasonAsync(
        long listingId, CancellationToken cancellationToken = default)
    {
        var reviewReason = await dbContext.Set<ListingModerationReview>()
            .AsNoTracking()
            .Where(r => r.ListingId == listingId && r.Decision == ModerationDecision.Rejected)
            .OrderByDescending(r => r.ReviewId)
            .Select(r => r.ReasonsJson)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(reviewReason))
        {
            return reviewReason;
        }

        return await dbContext.AmeModerationResults
            .AsNoTracking()
            .Where(r => r.ListingId == listingId && r.Decision == ModerationDecision.Rejected)
            .OrderByDescending(r => r.ResultId)
            .Select(r => r.ReasonsJson)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, string>> GetLatestRejectReasonsAsync(
        IReadOnlyCollection<long> listingIds, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<long, string>();
        if (listingIds.Count == 0)
        {
            return result;
        }

        var reviews = await dbContext.Set<ListingModerationReview>()
            .AsNoTracking()
            .Where(r => listingIds.Contains(r.ListingId) && r.Decision == ModerationDecision.Rejected)
            .Select(r => new { r.ListingId, r.ReviewId, r.ReasonsJson })
            .ToListAsync(cancellationToken);

        foreach (var group in reviews.GroupBy(r => r.ListingId))
        {
            var latest = group.OrderByDescending(x => x.ReviewId).FirstOrDefault();
            if (latest is not null && !string.IsNullOrWhiteSpace(latest.ReasonsJson))
            {
                result[group.Key] = latest.ReasonsJson;
            }
        }

        var missingIds = listingIds.Where(id => !result.ContainsKey(id)).ToArray();
        if (missingIds.Length > 0)
        {
            var ameResults = await dbContext.AmeModerationResults
                .AsNoTracking()
                .Where(r => missingIds.Contains(r.ListingId) && r.Decision == ModerationDecision.Rejected)
                .Select(r => new { r.ListingId, r.ResultId, r.ReasonsJson })
                .ToListAsync(cancellationToken);

            foreach (var group in ameResults.GroupBy(r => r.ListingId))
            {
                var latest = group.OrderByDescending(x => x.ResultId).FirstOrDefault();
                if (latest is not null && !string.IsNullOrWhiteSpace(latest.ReasonsJson))
                {
                    result[group.Key] = latest.ReasonsJson;
                }
            }
        }

        return result;
    }

    public async Task<string?> GetLatestModerationWarningAsync(
        long listingId, CancellationToken cancellationToken = default)
    {
        var ameResult = await GetLatestAmeResultAsync(listingId, cancellationToken);
        return ExtractPriceWarningMessage(ameResult);
    }

    public async Task<IReadOnlyDictionary<long, string>> GetLatestModerationWarningsAsync(
        IReadOnlyCollection<long> listingIds, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<long, string>();
        if (listingIds.Count == 0)
        {
            return result;
        }

        var ameRows = await dbContext.AmeModerationResults
            .AsNoTracking()
            .Where(r => listingIds.Contains(r.ListingId))
            .ToListAsync(cancellationToken);

        foreach (var group in ameRows.GroupBy(r => r.ListingId))
        {
            var latest = group.OrderByDescending(r => r.ResultId).FirstOrDefault();
            var warning = ExtractPriceWarningMessage(latest);
            if (!string.IsNullOrWhiteSpace(warning))
            {
                result[group.Key] = warning;
            }
        }

        return result;
    }

    private static string? ExtractPriceWarningMessage(AmeModerationResult? ameResult)
    {
        if (ameResult is null || ameResult.Decision != ModerationDecision.Approved)
        {
            return null;
        }

        var hasPriceWarning =
            (!string.IsNullOrWhiteSpace(ameResult.WarningsJson) &&
             ameResult.WarningsJson.Contains("PRICE_", StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(ameResult.ReasonsJson) &&
             ameResult.ReasonsJson.Contains("PRICE_", StringComparison.OrdinalIgnoreCase));

        if (!hasPriceWarning)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(ameResult.ReasonsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(ameResult.ReasonsJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("PriceWarningMessage", out var msgElem) &&
                        msgElem.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(msgElem.GetString()))
                    {
                        return msgElem.GetString();
                    }

                    if (doc.RootElement.TryGetProperty("Summary", out var summaryElem) &&
                        summaryElem.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(summaryElem.GetString()))
                    {
                        return summaryElem.GetString();
                    }
                }
            }
            catch (JsonException)
            {
                // Fall back to canonical price warning message below.
            }
        }

        return "Giá đăng chênh lệch lớn so với giá bình quân (m²) khu vực — tin vẫn được duyệt nhưng có thể là giá ảo.";
    }
}
