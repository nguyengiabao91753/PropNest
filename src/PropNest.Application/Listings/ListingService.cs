using System.Text.Json;
using PropNest.Application.Abstractions;
using PropNest.Application.Common;
using PropNest.Application.Listings.Search;
using PropNest.Domain.Common;
using PropNest.Domain.Listings;
using PropNest.Domain.Workflows;

namespace PropNest.Application.Listings;

public sealed class ListingService(
    IListingRepository listingRepository,
    IUnitOfWork unitOfWork,
    IMeilisearchListingSearchService? searchService = null) : IListingService
{
    public async Task<ListingDto> CreateAsync(CreateListingCommand command, CancellationToken cancellationToken = default)
    {
        var listing = new Listing(
            command.OwnerUserId,
            command.Title,
            command.Description,
            command.PropertyType,
            command.ListingType,
            command.Price,
            command.Area,
            command.City,
            command.District,
            command.Ward,
            command.Address,
            command.Floors,
            command.Bedrooms,
            command.Bathrooms,
            command.BalconyDirection,
            command.AdministrativeMode,
            command.ProvinceCode,
            command.DistrictCode,
            command.WardCode,
            command.LegacyAddress);

        await listingRepository.AddAsync(listing, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        listingRepository.AddHistory(ListingHistory.CreateSnapshot(
            listing.ListingId,
            command.OwnerUserId,
            "Create",
            JsonSerializer.Serialize(ToDto(listing))));

        if (listing.Status == ListingStatus.PendingModeration)
        {
            EnqueueListingSubmittedOutbox(listing);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(listing);
    }

    public async Task<ListingDto?> GetByIdAsync(long listingId, CancellationToken cancellationToken = default)
    {
        var listing = await listingRepository.GetByIdAsync(listingId, cancellationToken);
        if (listing is null) return null;

        var owner = await listingRepository.GetOwnerContactAsync(listing.OwnerUserId, cancellationToken);

        string? rejectReason = null;
        string? moderationWarning = null;
        if (listing.Status == ListingStatus.Rejected)
        {
            rejectReason = NormalizeOptionalReason(await listingRepository.GetLatestRejectReasonAsync(listingId, cancellationToken));
        }
        else if (listing.ModerationDecision == ModerationDecision.Approved)
        {
            moderationWarning = NormalizeOptionalReason(await listingRepository.GetLatestModerationWarningAsync(listingId, cancellationToken));
        }

        return ToDto(listing, owner, rejectReason, moderationWarning);
    }

    public async Task<ListingCollectionDto> GetByOwnerAsync(long ownerUserId, CancellationToken cancellationToken = default)
    {
        var listings = await listingRepository.GetByOwnerUserIdAsync(ownerUserId, cancellationToken);
        if (listings.Count == 0)
        {
            return new ListingCollectionDto(0, Array.Empty<ListingDto>());
        }

        var rejectedIds = listings
            .Where(l => l.Status == ListingStatus.Rejected)
            .Select(l => l.ListingId)
            .ToArray();

        var approvedIds = listings
            .Where(l => l.Status != ListingStatus.Rejected && l.ModerationDecision == ModerationDecision.Approved)
            .Select(l => l.ListingId)
            .ToArray();

        IReadOnlyDictionary<long, string>? rejectReasons = null;
        if (rejectedIds.Length > 0)
        {
            rejectReasons = await listingRepository.GetLatestRejectReasonsAsync(rejectedIds, cancellationToken);
        }

        IReadOnlyDictionary<long, string>? moderationWarnings = null;
        if (approvedIds.Length > 0)
        {
            moderationWarnings = await listingRepository.GetLatestModerationWarningsAsync(approvedIds, cancellationToken);
        }

        var items = listings
            .Select(l =>
            {
                string? rejectReason = null;
                rejectReasons?.TryGetValue(l.ListingId, out rejectReason);
                string? moderationWarning = null;
                moderationWarnings?.TryGetValue(l.ListingId, out moderationWarning);
                return ToDto(
                    l,
                    null,
                    NormalizeOptionalReason(rejectReason),
                    NormalizeOptionalReason(moderationWarning));
            })
            .ToList();

        return new ListingCollectionDto(items.Count, items);
    }

    public async Task<ListingCollectionDto> GetPendingModerationAsync(CancellationToken cancellationToken = default)
    {
        var items = await listingRepository.GetPendingModerationWithOwnersAsync(cancellationToken);
        return new ListingCollectionDto(items.Count, items);
    }

    public async Task<ListingDto> UpdateAsync(UpdateListingCommand command, CancellationToken cancellationToken = default)
    {
        var listing = await GetOwnedListingAsync(command.ListingId, command.OwnerUserId, cancellationToken);
        var before = ToDto(listing);
        listingRepository.SetOriginalRowVersion(listing, command.RowVersion);

        listing.UpdateContent(
            command.Title,
            command.Description,
            command.PropertyType,
            command.ListingType,
            command.Price,
            command.Area,
            command.City,
            command.District,
            command.Ward,
            command.Address,
            command.Floors,
            command.Bedrooms,
            command.Bathrooms,
            command.BalconyDirection,
            command.AdministrativeMode,
            command.ProvinceCode,
            command.DistrictCode,
            command.WardCode,
            command.LegacyAddress);

        var after = ToDto(listing);
        var deltas = DeltaEngine.CreateDeltas(before, after);
        listingRepository.AddHistory(ListingHistory.CreateDelta(
            listing.ListingId,
            command.OwnerUserId,
            "UpdateContent",
            JsonSerializer.Serialize(deltas)));

        if (listing.Status == ListingStatus.PendingModeration)
        {
            EnqueueListingSubmittedOutbox(listing);
        }

        // Lưu toàn bộ thay đổi tin đăng + lịch sử Delta + Outbox AME trong 1 lần SaveChanges nguyên tử
        var syncPayload = JsonSerializer.Serialize(new { listing.ListingId, listing.OwnerUserId });
        listingRepository.AddOutboxMessage(new OutboxMessage("ListingSearchSyncRequested", syncPayload));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(listing);
    }

    public async Task SubmitAsync(long listingId, long ownerUserId, CancellationToken cancellationToken = default)
    {
        var listing = await GetOwnedListingAsync(listingId, ownerUserId, cancellationToken);
        listing.SubmitForModeration();
        listingRepository.AddHistory(ListingHistory.CreateEvent(listingId, ownerUserId, "Submit"));
        EnqueueListingSubmittedOutbox(listing);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Listing> GetOwnedListingAsync(long listingId, long ownerUserId, CancellationToken cancellationToken)
    {
        var listing = await listingRepository.GetByIdAsync(listingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Listing {listingId} was not found.");

        if (listing.OwnerUserId != ownerUserId)
        {
            throw new UnauthorizedAccessException("The current user does not own this listing.");
        }

        return listing;
    }

    private void EnqueueListingSubmittedOutbox(Listing listing)
    {
        var correlationId = Guid.NewGuid();
        var imageUrls = listing.Images?
            .OrderBy(img => img.IsCover ? 0 : 1)
            .ThenBy(img => img.SortOrder)
            .Select(img => img.Url)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToList() ?? [];

        var submittedEvent = new ListingSubmitted(
            CorrelationId: correlationId,
            ListingId: listing.ListingId,
            OwnerUserId: listing.OwnerUserId,
            Title: listing.Title,
            Description: listing.Description,
            PropertyType: listing.PropertyType,
            ListingType: listing.ListingType,
            Price: listing.Price,
            Area: listing.Area,
            City: listing.City,
            District: listing.District,
            Ward: listing.Ward,
            Address: listing.Address,
            SubmittedAt: DateTimeOffset.UtcNow,
            Images: imageUrls,
            LegacyAddress: listing.LegacyAddress,
            AdministrativeMode: listing.AdministrativeMode,
            ProvinceCode: listing.ProvinceCode,
            Floors: listing.Floors,
            Bedrooms: listing.Bedrooms,
            Bathrooms: listing.Bathrooms);

        var payloadJson = JsonSerializer.Serialize(submittedEvent);
        listingRepository.AddOutboxMessage(
            new OutboxMessage("ListingSubmitted", payloadJson, correlationId));
    }
    private static ListingDto ToDto(Listing listing) => new(
        listing.ListingId,
        listing.OwnerUserId,
        listing.Title,
        listing.Description,
        listing.PropertyType,
        listing.ListingType,
        listing.Price,
        listing.Area,
        listing.City,
        listing.District,
        listing.Ward,
        listing.Address,
        listing.Status,
        listing.ModerationDecision,
        listing.PackageCode,
        listing.StartDate,
        listing.EndDate,
        listing.RowVersion,
        null,
        null,
        null,
        null,
        listing.Images?.Select(img => img.Url).ToList() ?? new List<string>(),
        listing.Floors,
        listing.Bedrooms,
        listing.Bathrooms,
        listing.BalconyDirection,
        listing.AdministrativeMode,
        listing.ProvinceCode,
        listing.DistrictCode,
        listing.WardCode,
        listing.LegacyAddress,
        listing.PackageEndDate);

    private static ListingDto ToDto(
        Listing listing,
        ListingOwnerContactDto? owner = null,
        string? rejectReason = null,
        string? moderationWarning = null) => new(
        listing.ListingId,
        listing.OwnerUserId,
        listing.Title,
        listing.Description,
        listing.PropertyType,
        listing.ListingType,
        listing.Price,
        listing.Area,
        listing.City,
        listing.District,
        listing.Ward,
        listing.Address,
        listing.Status,
        listing.ModerationDecision,
        listing.PackageCode,
        listing.StartDate,
        listing.EndDate,
        listing.RowVersion,
        owner?.FullName,
        owner?.Email,
        owner?.PhoneNumber,
        listing.CreatedAt,
        listing.Images?.Select(img => img.Url).ToList() ?? new List<string>(),
        listing.Floors,
        listing.Bedrooms,
        listing.Bathrooms,
        listing.BalconyDirection,
        listing.AdministrativeMode,
        listing.ProvinceCode,
        listing.DistrictCode,
        listing.WardCode,
        listing.LegacyAddress,
        listing.PackageEndDate,
        rejectReason,
        moderationWarning);

    public async Task<PagedResult<ListingSummaryDto>> GetPublicListingsAsync(GetPublicListingsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query, nameof(query));

        var pageNumber = query.Page ?? query.PageNumber;
        if (pageNumber < 1) pageNumber = 1;
        var pageSize = query.PageSize < 1 ? 1 :
                       query.PageSize > 50 ? 50 : query.PageSize;

        var normalizedQuery = query with
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        if (searchService is not null)
        {
            return await searchService.SearchListingsAsync(normalizedQuery, cancellationToken);
        }

        return await listingRepository.GetPublicListingsAsync(normalizedQuery, cancellationToken);
    }

    public async Task HideAsync(long listingId, long ownerUserId, CancellationToken cancellationToken = default)
    {
        var listing = await GetOwnedListingAsync(listingId, ownerUserId, cancellationToken);
        if (listing is null)
        {
            throw new KeyNotFoundException($"Listing {listingId} was not found or does not belong to the user {ownerUserId}.");
        }

        if (listing.OwnerUserId != ownerUserId)
        {
            throw new UnauthorizedAccessException($"User {ownerUserId} does not have permission to hide listing {listingId}.");
        }

        if (listing.Status != ListingStatus.Published)
        {
            throw new ArgumentException("Only a published listing can be hidden.");
        }

        var preHideSnapshot = JsonSerializer.Serialize(new { PreviousStatus = listing.Status.ToString() });
        listing.Hide();
        listingRepository.AddHistory(ListingHistory.CreateSnapshot(listingId, ownerUserId, "Hide", preHideSnapshot));
        var hidePayload = JsonSerializer.Serialize(new { listing.ListingId, listing.OwnerUserId });
        listingRepository.AddOutboxMessage(new OutboxMessage("ListingHidden", hidePayload));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ListingDto> UnhideAsync(long listingId, long ownerUserId, CancellationToken cancellationToken = default)
    {
        var listing = await GetOwnedListingAsync(listingId, ownerUserId, cancellationToken);
        if (listing.Status != ListingStatus.Hidden)
        {
            throw new InvalidOperationException("Only a hidden listing can be restored.");
        }

        var histories = await listingRepository.GetHistoriesByListingIdAsync(listingId, cancellationToken);
        var targetStatus = ResolvePreHideStatus(listing, histories);

        listing.Unhide(targetStatus, DateTimeOffset.UtcNow);
        var restoreSnapshot = JsonSerializer.Serialize(new { RestoredStatus = listing.Status.ToString() });
        listingRepository.AddHistory(ListingHistory.CreateSnapshot(
            listingId,
            ownerUserId,
            "Unhide",
            restoreSnapshot,
            "Khôi phục hiển thị tin đăng."));
        var unhidePayload = JsonSerializer.Serialize(new { listing.ListingId, listing.OwnerUserId });
        listingRepository.AddOutboxMessage(new OutboxMessage("ListingRestored", unhidePayload));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(listing);
    }

    private static ListingStatus ResolvePreHideStatus(Listing listing, IReadOnlyList<ListingHistoryDto> histories)
    {
        var lastHideIndex = -1;
        for (var i = histories.Count - 1; i >= 0; i--)
        {
            if (string.Equals(histories[i].ActionType, "Hide", StringComparison.OrdinalIgnoreCase))
            {
                lastHideIndex = i;
                break;
            }
        }

        if (lastHideIndex >= 0 &&
            TryParseStatusFromSnapshot(histories[lastHideIndex].SnapshotJson, "PreviousStatus", out var snapshotStatus))
        {
            return snapshotStatus;
        }

        var limit = lastHideIndex >= 0 ? lastHideIndex : histories.Count;
        ListingStatus? inferred = null;

        for (var i = 0; i < limit; i++)
        {
            var entry = histories[i];
            inferred = entry.ActionType switch
            {
                "Create" => ListingStatus.Draft,
                "Submit" => ListingStatus.PendingModeration,
                "Approve" or "AutoApprove" => ListingStatus.Published,
                "Reject" or "AutoReject" => ListingStatus.Rejected,
                "Expire" => ListingStatus.Expired,
                "Hide" => ListingStatus.Hidden,
                "Unhide" => TryParseStatusFromSnapshot(entry.SnapshotJson, "RestoredStatus", out var restored)
                    ? restored
                    : inferred,
                "UpdateContent" => inferred is ListingStatus.Published or ListingStatus.Hidden
                    ? ListingStatus.PendingModeration
                    : (inferred ?? ListingStatus.Draft),
                "UpgradePackage" => listing.ModerationDecision == ModerationDecision.Approved || inferred == ListingStatus.Published
                    ? ListingStatus.Published
                    : ListingStatus.PendingModeration,
                _ => inferred
            };
        }

        if (inferred is ListingStatus valid && valid is not (ListingStatus.Hidden or ListingStatus.Deleted))
        {
            return valid;
        }

        if (listing.ModerationDecision == ModerationDecision.Approved)
        {
            return ListingStatus.Published;
        }

        return ListingStatus.Draft;
    }

    private static bool TryParseStatusFromSnapshot(string? snapshotJson, string propertyName, out ListingStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(snapshotJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                        prop.Value.ValueKind == JsonValueKind.String &&
                        Enum.TryParse<ListingStatus>(prop.Value.GetString(), ignoreCase: true, out var parsed) &&
                        parsed is not (ListingStatus.Hidden or ListingStatus.Deleted))
                    {
                        status = parsed;
                        return true;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Ignore malformed legacy snapshot JSON and fall back to history replay.
        }

        return false;
    }

    public async Task<ListingDto> ApproveAsync(long listingId, long moderatorUserId, CancellationToken cancellationToken = default)
    {
        var listing = await listingRepository.GetByIdAsync(listingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Listing {listingId} was not found.");

        listing.Approve(DateTimeOffset.UtcNow);

        listingRepository.AddModerationReview(new ListingModerationReview(
            listing.ListingId,
            "Manual",
            ModerationDecision.Approved,
            null,
            moderatorUserId));

        listingRepository.AddHistory(ListingHistory.CreateEvent(
            listing.ListingId,
            moderatorUserId,
            "Approve",
            ModerationTextFormatter.ApprovedHistoryNote));

        var outboxType = listing.Status == ListingStatus.Published
            ? "ListingPublished"
            : "ListingApprovedPendingPayment";

        var payload = JsonSerializer.Serialize(new
        {
            listing.ListingId,
            listing.OwnerUserId,
            listing.PackageCode,
            Status = listing.Status.ToString(),
            listing.StartDate,
            listing.EndDate,
            listing.PackageEndDate
        });
        listingRepository.AddOutboxMessage(new OutboxMessage(outboxType, payload));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(listing);
    }

    public async Task<ListingDto> RejectAsync(long listingId, long moderatorUserId, string reasonsJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reasonsJson))
        {
            throw new ArgumentException("Rejection reasons are required.", nameof(reasonsJson));
        }

        var listing = await listingRepository.GetByIdAsync(listingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Listing {listingId} was not found.");

        listing.Reject();
        listingRepository.AddModerationReview(new ListingModerationReview(
            listing.ListingId,
            "Manual",
            ModerationDecision.Rejected,
            reasonsJson,
            moderatorUserId));
        var formattedReason = ModerationTextFormatter.FormatModerationReasonText(reasonsJson);
        if (string.IsNullOrWhiteSpace(formattedReason))
        {
            formattedReason = "Tin đăng chưa đáp ứng tiêu chuẩn nội dung.";
        }
        var historyNote = formattedReason.Length <= 480
            ? formattedReason
            : string.Concat(formattedReason.AsSpan(0, 477), "...");

        listingRepository.AddHistory(ListingHistory.CreateEvent(
            listing.ListingId,
            moderatorUserId,
            "Reject",
            historyNote));

        var payload = JsonSerializer.Serialize(new
        {
            listing.ListingId,
            listing.OwnerUserId,
            ReasonsJson = reasonsJson
        });
        listingRepository.AddOutboxMessage(new OutboxMessage("ListingRejected", payload));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(listing);
    }

    public async Task<IReadOnlyList<ListingHistoryDto>> GetHistoriesAsync(long listingId, CancellationToken cancellationToken = default)
    {
        var histories = await listingRepository.GetHistoriesByListingIdAsync(listingId, cancellationToken);
        var normalized = new List<ListingHistoryDto>(histories.Count);

        foreach (var h in histories)
        {
            // Bỏ qua các bản ghi nội bộ khi kiểm duyệt tự động không thể duyệt và chuyển duyệt thủ công
            if (ModerationTextFormatter.IsInternalFailureHistoryAction(h.ActionType))
            {
                continue;
            }

            if (ModerationTextFormatter.IsApprovalAction(h.ActionType))
            {
                var approvalNote = !string.IsNullOrWhiteSpace(h.Note)
                    && h.Note.Contains("giá ảo", StringComparison.OrdinalIgnoreCase)
                    ? ModerationTextFormatter.FormatModerationReasonText(h.Note)
                    : ModerationTextFormatter.ApprovedHistoryNote;

                var entry = h with
                {
                    ActionType = "Approve",
                    ActorUserId = null,
                    ActorName = null,
                    ActorRole = null,
                    Note = approvalNote
                };

                // Chỉ giữ 1 bản ghi duyệt cho mỗi chu kỳ phê duyệt (không trùng liên tiếp)
                if (normalized.Count > 0 && string.Equals(normalized[^1].ActionType, "Approve", StringComparison.OrdinalIgnoreCase))
                {
                    normalized[^1] = entry;
                }
                else
                {
                    normalized.Add(entry);
                }

                continue;
            }

            if (ModerationTextFormatter.IsRejectionAction(h.ActionType))
            {
                normalized.Add(h with
                {
                    ActionType = "Reject",
                    ActorUserId = null,
                    ActorName = null,
                    ActorRole = null,
                    Note = NormalizeOptionalReason(h.Note)
                });
                continue;
            }

            normalized.Add(h with
            {
                Note = string.IsNullOrWhiteSpace(h.Note)
                    ? h.Note
                    : ModerationTextFormatter.SanitizePlainText(h.Note)
            });
        }

        return normalized;
    }

    private static string? NormalizeOptionalReason(string? raw)
    {
        var formatted = ModerationTextFormatter.FormatModerationReasonText(raw);
        return string.IsNullOrWhiteSpace(formatted) ? null : formatted;
    }

    public async Task<PackageDto?> GetPackageByCodeAsync(string packageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageCode))
        {
            return null;
        }

        var package = await listingRepository.GetPackageByCodeAsync(packageCode.Trim(), cancellationToken);
        return package is null
            ? null
            : new PackageDto(package.PackageCode, package.Name, package.Price, package.DurationDays, package.Kind, package.IsActive);
    }

    public async Task<IReadOnlyList<PackageDto>> GetActivePackagesAsync(CancellationToken cancellationToken = default)
    {
        var packages = await listingRepository.GetActivePackagesAsync(cancellationToken);
        return packages
            .Where(p => ListingPackagePolicy.IsPurchasablePackage(p.Kind, p.Price, p.IsActive))
            .OrderBy(p => string.Equals(p.Kind, ListingPackagePolicy.StandardPackageCode, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(p => p.DurationDays)
            .Select(p => new PackageDto(p.PackageCode, p.Name, p.Price, p.DurationDays, p.Kind, p.IsActive))
            .ToList();
    }

    public async Task DeleteAsync(long listingId, long ownerUserId, CancellationToken cancellationToken = default)
    {
        var listing = await GetOwnedListingAsync(listingId, ownerUserId, cancellationToken);
        if (listing is null)
        {
            throw new KeyNotFoundException($"Listing {listingId} was not found or does not belong to the user {ownerUserId}.");
        }

        if (listing.OwnerUserId != ownerUserId)
        {
            throw new UnauthorizedAccessException($"User {ownerUserId} does not have permission to delete listing {listingId}.");
        }

        if (listing.Status != ListingStatus.Draft)
        {
            throw new ArgumentException("Only a draft listing can be deleted.");
        }

        listing.Delete();
        listingRepository.AddHistory(ListingHistory.CreateEvent(listingId, ownerUserId, "Delete"));
        var deletePayload = JsonSerializer.Serialize(new { listing.ListingId, listing.OwnerUserId });
        listingRepository.AddOutboxMessage(new OutboxMessage("ListingDeleted", deletePayload));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<AmeModerationResultDto?> GetAmeResultAsync(
    long listingId, CancellationToken cancellationToken = default)
    {
        var result = await listingRepository.GetLatestAmeResultAsync(listingId, cancellationToken);
        if (result is null) return null;

        return new AmeModerationResultDto(
            result.ListingId,
            result.Decision.ToString(),
            result.ScoreJson,
            result.ReasonsJson,
            result.WarningsJson,
            result.ModelInfo,
            result.CheckedAt);
    }
}
