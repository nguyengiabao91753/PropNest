namespace PropNest.Application.Listings;

public sealed record ListingImageDto(
    long ImageId,
    long ListingId,
    string Url,
    int SortOrder,
    bool IsCover,
    string? Caption,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record UpdateListingImageRequest(
    int? SortOrder,
    bool? IsCover,
    string? Caption);

public sealed record UploadImageItem(
    Stream ContentStream,
    string FileName,
    string ContentType,
    string? Caption = null);

public interface IListingImageService
{
    Task<IReadOnlyList<ListingImageDto>> UploadImagesAsync(
        long listingId,
        long userId,
        IReadOnlyList<UploadImageItem> files,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingImageDto>> GetImagesByListingIdAsync(
        long listingId,
        CancellationToken cancellationToken = default);

    Task<ListingImageDto> UpdateImageAsync(
        long listingId,
        long imageId,
        long userId,
        UpdateListingImageRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteImageAsync(
        long listingId,
        long imageId,
        long userId,
        CancellationToken cancellationToken = default);
}
