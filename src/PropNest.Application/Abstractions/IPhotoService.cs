namespace PropNest.Application.Abstractions;

public sealed record PhotoUploadResult(string Url, string PublicId);

public interface IPhotoService
{
    Task<PhotoUploadResult> UploadPhotoAsync(Stream stream, string fileName, string folder, CancellationToken cancellationToken = default);
    Task<bool> DeletePhotoAsync(string publicId, CancellationToken cancellationToken = default);
}
