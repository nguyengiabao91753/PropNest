using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PropNest.Application.Abstractions;

namespace PropNest.Infrastructure.Services;

public sealed partial class CloudinaryPhotoService : IPhotoService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryPhotoService> _logger;

    public CloudinaryPhotoService(IConfiguration configuration, ILogger<CloudinaryPhotoService> logger)
    {
        _logger = logger;

        var cloudName = configuration["Cloudinary:CloudName"]
            ?? throw new InvalidOperationException("Cloudinary:CloudName is not configured.");
        var apiKey = configuration["Cloudinary:ApiKey"]
            ?? throw new InvalidOperationException("Cloudinary:ApiKey is not configured.");
        var apiSecret = configuration["Cloudinary:ApiSecret"]
            ?? throw new InvalidOperationException("Cloudinary:ApiSecret is not configured.");

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
    }

    public async Task<PhotoUploadResult> UploadPhotoAsync(
        Stream stream,
        string fileName,
        string folder,
        CancellationToken cancellationToken = default)
    {
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, stream),
            Folder = folder,
            Transformation = new Transformation().Quality("auto").FetchFormat("auto")
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
        if (uploadResult.Error is not null)
        {
            LogUploadError(uploadResult.Error.Message);
            throw new InvalidOperationException($"Cloudinary upload failed: {uploadResult.Error.Message}");
        }

        return new PhotoUploadResult(
            uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString() ?? string.Empty,
            uploadResult.PublicId);
    }

    public async Task<bool> DeletePhotoAsync(string publicId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicId))
        {
            return false;
        }

        var deletionParams = new DeletionParams(publicId);
        var result = await _cloudinary.DestroyAsync(deletionParams);

        if (result.Error is not null)
        {
            LogDeleteError(result.Error.Message);
            return false;
        }

        return string.Equals(result.Result, "ok", StringComparison.OrdinalIgnoreCase);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Cloudinary upload error: {ErrorMessage}")]
    private partial void LogUploadError(string errorMessage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cloudinary delete error: {ErrorMessage}")]
    private partial void LogDeleteError(string errorMessage);
}
