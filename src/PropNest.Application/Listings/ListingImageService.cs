using System.Text.Json;
using PropNest.Application.Abstractions;
using PropNest.Domain.Listings;
using PropNest.Domain.Workflows;

namespace PropNest.Application.Listings;

public sealed class ListingImageService(
    IListingRepository listingRepository,
    IPhotoService photoService,
    IUnitOfWork unitOfWork) : IListingImageService
{
    public const int MaxImagesPerListing = 15;

    public async Task<IReadOnlyList<ListingImageDto>> UploadImagesAsync(
        long listingId,
        long userId,
        IReadOnlyList<UploadImageItem> files,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        if (files.Count == 0)
        {
            throw new ArgumentException("At least one image file is required.", nameof(files));
        }

        var listing = await listingRepository.GetByIdWithImagesAsync(listingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Listing with ID {listingId} was not found.");

        EnsureOwner(listing, userId, isAdmin);
        EnsureCanModifyImages(listing);

        var existingImages = listing.Images.OrderBy(x => x.SortOrder).ToList();
        if (existingImages.Count + files.Count > MaxImagesPerListing)
        {
            throw new ArgumentException(
                $"Mỗi tin đăng chỉ được chứa tối đa {MaxImagesPerListing} hình ảnh (hiện có {existingImages.Count} ảnh).",
                nameof(files));
        }

        var nextSortOrder = existingImages.Count > 0 ? existingImages.Max(x => x.SortOrder) + 1 : 0;
        var hasCover = existingImages.Any(x => x.IsCover);
        var uploadedPublicIds = new List<string>(files.Count);
        var addedImages = new List<ListingImage>(files.Count);

        try
        {
            for (var i = 0; i < files.Count; i++)
            {
                var item = files[i];
                var folder = $"propnest/listings/{listingId}";
                var uploadResult = await photoService.UploadPhotoAsync(item.ContentStream, item.FileName, folder, cancellationToken);
                uploadedPublicIds.Add(uploadResult.PublicId);

                var isCover = !hasCover && i == 0;
                var image = new ListingImage(
                    listingId,
                    uploadResult.Url,
                    uploadResult.PublicId,
                    nextSortOrder++,
                    isCover,
                    item.Caption);

                listingRepository.AddImage(image);
                addedImages.Add(image);
            }

            var wasPublished = listing.Status == ListingStatus.Published;
            if (listing.MarkPendingModerationAfterMediaChange())
            {
                var allImageUrls = existingImages
                    .Concat(addedImages)
                    .OrderBy(img => img.IsCover ? 0 : 1)
                    .ThenBy(img => img.SortOrder)
                    .Select(img => img.Url)
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .ToList();

                listingRepository.AddHistory(ListingHistory.CreateEvent(
                    listingId,
                    userId,
                    "UpdateImages",
                    $"Tải lên {files.Count} hình ảnh mới và gửi kiểm duyệt lại."));

                EnqueueListingSubmittedOutbox(listing, allImageUrls);
            }

            if (wasPublished || listing.Status == ListingStatus.Published)
            {
                var mediaPayload = JsonSerializer.Serialize(new { listing.ListingId, listing.OwnerUserId });
                listingRepository.AddOutboxMessage(new OutboxMessage("ListingMediaUpdated", mediaPayload));
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (var publicId in uploadedPublicIds)
            {
                try
                {
                    await photoService.DeletePhotoAsync(publicId, CancellationToken.None);
                }
                catch
                {
                    // Best-effort cleanup of uploaded assets when transaction fails.
                }
            }

            throw;
        }

        var updatedImages = await listingRepository.GetImagesByListingIdAsync(listingId, cancellationToken);
        return updatedImages.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<ListingImageDto>> GetImagesByListingIdAsync(
        long listingId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);

        _ = await listingRepository.GetByIdAsync(listingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Listing with ID {listingId} was not found.");

        var images = await listingRepository.GetImagesByListingIdAsync(listingId, cancellationToken);
        return images.Select(ToDto).ToList();
    }

    public async Task<ListingImageDto> UpdateImageAsync(
        long listingId,
        long imageId,
        long userId,
        UpdateListingImageRequest request,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        var listing = await listingRepository.GetByIdAsync(listingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Listing with ID {listingId} was not found.");

        EnsureOwner(listing, userId, isAdmin);
        EnsureCanModifyImages(listing);

        var image = await listingRepository.GetImageByIdAsync(imageId, cancellationToken)
            ?? throw new KeyNotFoundException($"Image with ID {imageId} was not found.");

        if (image.ListingId != listingId)
        {
            throw new InvalidOperationException("The specified image does not belong to this listing.");
        }

        if (request.IsCover.HasValue)
        {
            if (request.IsCover.Value)
            {
                // Dùng truy vấn có Tracking để EF Core thực sự lưu IsCover = false cho các ảnh khác
                var allTrackedImages = await listingRepository.GetTrackedImagesByListingIdAsync(listingId, cancellationToken);
                foreach (var other in allTrackedImages.Where(x => x.ImageId != imageId && x.IsCover))
                {
                    other.SetCover(false);
                }

                image.SetCover(true);
            }
            else
            {
                image.SetCover(false);
            }
        }

        if (request.SortOrder.HasValue)
        {
            image.SetSortOrder(request.SortOrder.Value);
        }

        if (request.Caption is not null)
        {
            image.SetCaption(request.Caption.Trim());
        }

        if (listing.Status == ListingStatus.Published)
        {
            var mediaPayload = JsonSerializer.Serialize(new { listing.ListingId, listing.OwnerUserId });
            listingRepository.AddOutboxMessage(new OutboxMessage("ListingMediaUpdated", mediaPayload));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(image);
    }

    public async Task<IReadOnlyList<ListingImageDto>> ReorderImagesAsync(
        long listingId,
        long userId,
        IReadOnlyList<long> orderedImageIds,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentNullException.ThrowIfNull(orderedImageIds);

        if (orderedImageIds.Count == 0)
        {
            throw new ArgumentException("At least one image ID is required to reorder images.", nameof(orderedImageIds));
        }

        var listing = await listingRepository.GetByIdWithImagesAsync(listingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Listing with ID {listingId} was not found.");

        EnsureOwner(listing, userId, isAdmin);
        EnsureCanModifyImages(listing);

        var existingById = listing.Images.ToDictionary(x => x.ImageId);
        var seen = new HashSet<long>();
        var ordered = new List<ListingImage>(existingById.Count);

        foreach (var imageId in orderedImageIds)
        {
            if (!seen.Add(imageId))
            {
                throw new ArgumentException($"Duplicate image ID {imageId} in reorder request.", nameof(orderedImageIds));
            }

            if (!existingById.TryGetValue(imageId, out var image))
            {
                throw new ArgumentException($"Image with ID {imageId} does not belong to listing {listingId}.", nameof(orderedImageIds));
            }

            ordered.Add(image);
        }

        // Giữ lại các ảnh không nằm trong danh sách theo thứ tự cũ ở cuối (nếu có)
        foreach (var remaining in listing.Images
            .Where(x => !seen.Contains(x.ImageId))
            .OrderBy(x => x.IsCover ? 0 : 1)
            .ThenBy(x => x.SortOrder))
        {
            ordered.Add(remaining);
        }

        var changed = false;
        for (var i = 0; i < ordered.Count; i++)
        {
            var shouldBeCover = i == 0;
            if (ordered[i].SortOrder != i || ordered[i].IsCover != shouldBeCover)
            {
                ordered[i].SetSortOrder(i);
                ordered[i].SetCover(shouldBeCover);
                changed = true;
            }
        }

        if (changed)
        {
            listing.RecordMediaUpdate();

            if (listing.Status == ListingStatus.Published)
            {
                var mediaPayload = JsonSerializer.Serialize(new { listing.ListingId, listing.OwnerUserId });
                listingRepository.AddOutboxMessage(new OutboxMessage("ListingMediaUpdated", mediaPayload));
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ordered.Select(ToDto).ToList();
    }

    public async Task<SyncListingImagesResult> SyncImagesAsync(
        long listingId,
        long userId,
        IReadOnlyList<string> orderTokens,
        IReadOnlyList<UploadImageItem> newFiles,
        byte[]? expectedRowVersion = null,
        bool deferReModeration = false,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentNullException.ThrowIfNull(orderTokens);
        ArgumentNullException.ThrowIfNull(newFiles);

        var listing = await listingRepository.GetByIdWithImagesAsync(listingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Listing with ID {listingId} was not found.");

        EnsureOwner(listing, userId, isAdmin);
        EnsureCanModifyImages(listing);

        if (expectedRowVersion is { Length: > 0 })
        {
            if (listing.RowVersion.Length > 0 && !listing.RowVersion.SequenceEqual(expectedRowVersion))
            {
                throw new ListingConcurrencyException("The listing has been modified by another request. Please refresh and try again.");
            }

            listingRepository.SetOriginalRowVersion(listing, expectedRowVersion);
        }

        if (orderTokens.Count > MaxImagesPerListing)
        {
            throw new ArgumentException(
                $"Mỗi tin đăng chỉ được chứa tối đa {MaxImagesPerListing} hình ảnh.",
                nameof(orderTokens));
        }

        var existingOrdered = listing.Images
            .OrderBy(x => x.IsCover ? 0 : 1)
            .ThenBy(x => x.SortOrder)
            .ToList();
        var existingById = existingOrdered.ToDictionary(x => x.ImageId);

        var parsedTokens = new List<(bool IsNew, long IdOrIndex)>(orderTokens.Count);
        var seenExisting = new HashSet<long>();
        var seenNewIndices = new HashSet<int>();

        foreach (var rawToken in orderTokens)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
            {
                throw new ArgumentException("Image order manifest contains an empty token.", nameof(orderTokens));
            }

            var token = rawToken.Trim();
            if (token.StartsWith("existing:", StringComparison.OrdinalIgnoreCase))
            {
                if (!long.TryParse(token["existing:".Length..], out var existingId) || !existingById.ContainsKey(existingId))
                {
                    throw new ArgumentException($"Ảnh hiện có '{token}' không thuộc tin đăng #{listingId}.", nameof(orderTokens));
                }

                if (!seenExisting.Add(existingId))
                {
                    throw new ArgumentException($"Trùng mã ảnh hiện có '{token}' trong thứ tự sắp xếp.", nameof(orderTokens));
                }

                parsedTokens.Add((false, existingId));
            }
            else if (token.StartsWith("new:", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(token["new:".Length..], out var fileIdx) || fileIdx < 0 || fileIdx >= newFiles.Count)
                {
                    throw new ArgumentException($"Chỉ mục ảnh mới '{token}' không hợp lệ.", nameof(orderTokens));
                }

                if (!seenNewIndices.Add(fileIdx))
                {
                    throw new ArgumentException($"Trùng chỉ mục ảnh mới '{token}' trong thứ tự sắp xếp.", nameof(orderTokens));
                }

                parsedTokens.Add((true, fileIdx));
            }
            else
            {
                throw new ArgumentException($"Định dạng mã thứ tự ảnh không hợp lệ: '{token}'.", nameof(orderTokens));
            }
        }

        if (seenNewIndices.Count != newFiles.Count)
        {
            throw new ArgumentException("Danh sách tệp ảnh mới không khớp với thứ tự sắp xếp đã gửi.", nameof(newFiles));
        }

        var removedImages = existingOrdered
            .Where(img => !seenExisting.Contains(img.ImageId))
            .ToList();

        var isOrderIdentical =
            newFiles.Count == 0 &&
            removedImages.Count == 0 &&
            parsedTokens.Count == existingOrdered.Count &&
            parsedTokens.Select((t, idx) =>
                !t.IsNew &&
                existingOrdered[idx].ImageId == t.IdOrIndex &&
                existingOrdered[idx].SortOrder == idx &&
                existingOrdered[idx].IsCover == (idx == 0)).All(ok => ok);

        if (isOrderIdentical)
        {
            return new SyncListingImagesResult(existingOrdered.Select(ToDto).ToList(), listing.RowVersion);
        }

        var uploadedPublicIds = new List<string>(newFiles.Count);
        var uploadedByIndex = new Dictionary<int, ListingImage>(newFiles.Count);

        try
        {
            var folder = $"propnest/listings/{listingId}";
            for (var i = 0; i < newFiles.Count; i++)
            {
                var item = newFiles[i];
                var uploadResult = await photoService.UploadPhotoAsync(item.ContentStream, item.FileName, folder, cancellationToken);
                uploadedPublicIds.Add(uploadResult.PublicId);

                var createdImage = new ListingImage(
                    listingId,
                    uploadResult.Url,
                    uploadResult.PublicId,
                    0,
                    false,
                    item.Caption);

                listingRepository.AddImage(createdImage);
                uploadedByIndex[i] = createdImage;
            }

            foreach (var removed in removedImages)
            {
                listingRepository.RemoveImage(removed);
            }

            var finalImages = new List<ListingImage>(parsedTokens.Count);
            for (var pos = 0; pos < parsedTokens.Count; pos++)
            {
                var (isNew, idOrIdx) = parsedTokens[pos];
                var img = isNew ? uploadedByIndex[(int)idOrIdx] : existingById[idOrIdx];
                img.SetSortOrder(pos);
                img.SetCover(pos == 0);
                finalImages.Add(img);
            }

            var hasPixelChanges = newFiles.Count > 0 || removedImages.Count > 0;
            var wasPublished = listing.Status == ListingStatus.Published;

            if (deferReModeration)
            {
                listing.RecordMediaUpdate();
                if (hasPixelChanges)
                {
                    listingRepository.AddHistory(ListingHistory.CreateEvent(
                        listingId,
                        userId,
                        "UpdateImages",
                        BuildImageChangeSummary(newFiles.Count, removedImages.Count, triggeredReModeration: false)));
                }
            }
            else if (hasPixelChanges)
            {
                if (listing.MarkPendingModerationAfterMediaChange())
                {
                    var finalUrls = finalImages
                        .Select(img => img.Url)
                        .Where(url => !string.IsNullOrWhiteSpace(url))
                        .ToList();

                    listingRepository.AddHistory(ListingHistory.CreateEvent(
                        listingId,
                        userId,
                        "UpdateImages",
                        BuildImageChangeSummary(newFiles.Count, removedImages.Count, triggeredReModeration: true)));

                    EnqueueListingSubmittedOutbox(listing, finalUrls);
                }
            }
            else
            {
                // Chỉ đổi thứ tự ảnh hiện có — không cần gửi kiểm duyệt lại
                listing.RecordMediaUpdate();
            }

            if (wasPublished || listing.Status == ListingStatus.Published)
            {
                var mediaPayload = JsonSerializer.Serialize(new { listing.ListingId, listing.OwnerUserId });
                listingRepository.AddOutboxMessage(new OutboxMessage("ListingMediaUpdated", mediaPayload));
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var removed in removedImages)
            {
                try
                {
                    await photoService.DeletePhotoAsync(removed.PublicId, CancellationToken.None);
                }
                catch
                {
                    // DB transaction đã thành công; lỗi xóa tệp trên CDN không làm hỏng trải nghiệm người dùng.
                }
            }

            var persisted = await listingRepository.GetImagesByListingIdAsync(listingId, cancellationToken);
            return new SyncListingImagesResult(persisted.Select(ToDto).ToList(), listing.RowVersion);
        }
        catch
        {
            foreach (var publicId in uploadedPublicIds)
            {
                try
                {
                    await photoService.DeletePhotoAsync(publicId, CancellationToken.None);
                }
                catch
                {
                    // Best-effort cleanup khi transaction thất bại.
                }
            }

            throw;
        }
    }

    private static string BuildImageChangeSummary(int addedCount, int removedCount, bool triggeredReModeration)
    {
        var parts = new List<string>();
        if (addedCount > 0) parts.Add($"thêm {addedCount} ảnh");
        if (removedCount > 0) parts.Add($"xóa {removedCount} ảnh");
        var actionText = parts.Count > 0 ? string.Join(", ", parts) : "cập nhật thứ tự ảnh";
        return triggeredReModeration
            ? $"Cập nhật hình ảnh ({actionText}) và gửi kiểm duyệt lại."
            : $"Cập nhật hình ảnh ({actionText}).";
    }

    public async Task DeleteImageAsync(
        long listingId,
        long imageId,
        long userId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        var listing = await listingRepository.GetByIdWithImagesAsync(listingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Listing with ID {listingId} was not found.");

        EnsureOwner(listing, userId, isAdmin);
        EnsureCanModifyImages(listing);

        var image = listing.Images.FirstOrDefault(x => x.ImageId == imageId)
            ?? await listingRepository.GetImageByIdAsync(imageId, cancellationToken)
            ?? throw new KeyNotFoundException($"Image with ID {imageId} was not found.");

        if (image.ListingId != listingId)
        {
            throw new InvalidOperationException("The specified image does not belong to this listing.");
        }

        var remainingImages = listing.Images
            .Where(x => x.ImageId != imageId)
            .OrderBy(x => x.SortOrder)
            .ToList();

        if (image.IsCover && remainingImages.Count > 0)
        {
            // remainingImages đã được EF Core track qua GetByIdWithImagesAsync -> lưu IsCover = true chuẩn xác
            remainingImages[0].SetCover(true);
        }

        listingRepository.RemoveImage(image);

        var wasPublished = listing.Status == ListingStatus.Published;
        if (listing.MarkPendingModerationAfterMediaChange())
        {
            var remainingUrls = remainingImages
                .OrderBy(img => img.IsCover ? 0 : 1)
                .ThenBy(img => img.SortOrder)
                .Select(img => img.Url)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToList();

            listingRepository.AddHistory(ListingHistory.CreateEvent(
                listingId,
                userId,
                "DeleteImage",
                "Xóa hình ảnh khỏi tin đăng và gửi kiểm duyệt lại."));

            EnqueueListingSubmittedOutbox(listing, remainingUrls);
        }

        if (wasPublished || listing.Status == ListingStatus.Published)
        {
            var mediaPayload = JsonSerializer.Serialize(new { listing.ListingId, listing.OwnerUserId });
            listingRepository.AddOutboxMessage(new OutboxMessage("ListingMediaUpdated", mediaPayload));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await photoService.DeletePhotoAsync(image.PublicId, cancellationToken);
        }
        catch
        {
            // DB transaction already succeeded; asset deletion failure shouldn't break user flow.
        }
    }

    private void EnqueueListingSubmittedOutbox(Listing listing, IReadOnlyList<string> imageUrls)
    {
        var correlationId = Guid.NewGuid();
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

        listingRepository.AddOutboxMessage(
            new OutboxMessage("ListingSubmitted", JsonSerializer.Serialize(submittedEvent), correlationId));
    }

    private static void EnsureOwner(Listing listing, long userId, bool isAdmin)
    {
        if (!isAdmin && listing.OwnerUserId != userId)
        {
            throw new UnauthorizedAccessException("You do not have permission to manage images for this listing.");
        }
    }

    private static void EnsureCanModifyImages(Listing listing)
    {
        if (listing.Status == ListingStatus.Deleted)
        {
            throw new InvalidOperationException("Cannot modify images for a deleted listing.");
        }

        if (listing.Status == ListingStatus.PendingModeration)
        {
            throw new InvalidOperationException("Cannot modify images while the listing is pending moderation.");
        }
    }

    private static ListingImageDto ToDto(ListingImage image) => new(
        image.ImageId,
        image.ListingId,
        image.Url,
        image.SortOrder,
        image.IsCover,
        image.Caption,
        image.Status,
        image.CreatedAt);
}
