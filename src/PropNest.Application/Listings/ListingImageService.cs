using PropNest.Application.Abstractions;
using PropNest.Domain.Listings;

namespace PropNest.Application.Listings;

public sealed class ListingImageService(
    IListingRepository listingRepository,
    IPhotoService photoService,
    IUnitOfWork unitOfWork) : IListingImageService
{
    public async Task<IReadOnlyList<ListingImageDto>> UploadImagesAsync(
        long listingId,
        long userId,
        IReadOnlyList<UploadImageItem> files,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        if (files.Count == 0)
        {
            throw new ArgumentException("At least one image file is required.", nameof(files));
        }

        var listing = await listingRepository.GetByIdAsync(listingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Listing with ID {listingId} was not found.");

        EnsureOwner(listing, userId);
        EnsureCanModifyImages(listing);

        var existingImages = await listingRepository.GetImagesByListingIdAsync(listingId, cancellationToken);
        var nextSortOrder = existingImages.Count > 0 ? existingImages.Max(x => x.SortOrder) + 1 : 0;
        var hasCover = existingImages.Any(x => x.IsCover);

        for (var i = 0; i < files.Count; i++)
        {
            var item = files[i];
            var folder = $"propnest/listings/{listingId}";
            var uploadResult = await photoService.UploadPhotoAsync(item.ContentStream, item.FileName, folder, cancellationToken);

            var isCover = !hasCover && i == 0;
            var image = new ListingImage(
                listingId,
                uploadResult.Url,
                uploadResult.PublicId,
                nextSortOrder++,
                isCover,
                item.Caption);

            listingRepository.AddImage(image);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var updatedImages = await listingRepository.GetImagesByListingIdAsync(listingId, cancellationToken);
        return updatedImages.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<ListingImageDto>> GetImagesByListingIdAsync(
        long listingId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);

        var listing = await listingRepository.GetByIdAsync(listingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Listing with ID {listingId} was not found.");

        var images = await listingRepository.GetImagesByListingIdAsync(listingId, cancellationToken);
        return images.Select(ToDto).ToList();
    }

    public async Task<ListingImageDto> UpdateImageAsync(
        long listingId,
        long imageId,
        long userId,
        UpdateListingImageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        var listing = await listingRepository.GetByIdAsync(listingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Listing with ID {listingId} was not found.");

        EnsureOwner(listing, userId);
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
                var allImages = await listingRepository.GetImagesByListingIdAsync(listingId, cancellationToken);
                foreach (var other in allImages.Where(x => x.ImageId != imageId && x.IsCover))
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

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(image);
    }

    public async Task DeleteImageAsync(
        long listingId,
        long imageId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        var listing = await listingRepository.GetByIdAsync(listingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Listing with ID {listingId} was not found.");

        EnsureOwner(listing, userId);
        EnsureCanModifyImages(listing);

        var image = await listingRepository.GetImageByIdAsync(imageId, cancellationToken)
            ?? throw new KeyNotFoundException($"Image with ID {imageId} was not found.");

        if (image.ListingId != listingId)
        {
            throw new InvalidOperationException("The specified image does not belong to this listing.");
        }

        await photoService.DeletePhotoAsync(image.PublicId, cancellationToken);
        listingRepository.RemoveImage(image);

        if (image.IsCover)
        {
            var remaining = await listingRepository.GetImagesByListingIdAsync(listingId, cancellationToken);
            var nextCover = remaining.FirstOrDefault(x => x.ImageId != imageId);
            nextCover?.SetCover(true);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureOwner(Listing listing, long userId)
    {
        if (listing.OwnerUserId != userId)
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
