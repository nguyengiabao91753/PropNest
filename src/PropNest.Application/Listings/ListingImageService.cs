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
                    $"Tải lên {files.Count} hình ảnh mới - tự động chuyển AME kiểm duyệt lại."));

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
                "Xóa hình ảnh khỏi tin đăng - tự động chuyển AME kiểm duyệt lại."));

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

        if (listing.Status == ListingStatus.Expired)
        {
            throw new InvalidOperationException("Cannot modify images for an expired listing.");
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
