using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropNest.Api.Extensions;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;
using PropNest.Domain.Listings;

namespace PropNest.Api.Controllers;

[ApiController]
[Route("api/v1/listings/{listingId:long}/images")]
public sealed class ListingImagesController(
    ICurrentUser currentUser,
    IListingService listingService,
    IListingImageService listingImageService) : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private const long MaxFileSizeInBytes = 10 * 1024 * 1024; // 10 MB
    private const int MaxFilesPerRequest = ListingImageService.MaxImagesPerListing;

    [HttpPost]
    [Authorize(Roles = "Seller, Admin")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<IReadOnlyList<ListingImageDto>>> UploadImages(
        long listingId,
        [FromForm] List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        var openedStreams = new List<Stream>();
        try
        {
            if (files is null || files.Count == 0)
            {
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["files"] = ["At least one image file is required."]
                }));
            }

            if (files.Count > MaxFilesPerRequest)
            {
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["files"] = [$"Chỉ được tải tối đa {MaxFilesPerRequest} hình ảnh mỗi lần."]
                }));
            }

            var (items, validationError) = await ReadAndValidateFilesAsync(files, openedStreams, cancellationToken);
            if (validationError is not null)
            {
                return ValidationProblem(validationError);
            }

            var userId = GetRequiredUserId();
            var isAdmin = currentUser.IsInRole("Admin");
            var images = await listingImageService.UploadImagesAsync(listingId, userId, items, isAdmin, cancellationToken);
            return CreatedAtAction(nameof(GetImages), new { listingId }, images);
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
        finally
        {
            foreach (var stream in openedStreams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    [HttpPost("sync")]
    [Authorize(Roles = "Seller, Admin")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<SyncListingImagesResult>> SyncImages(
        long listingId,
        [FromForm] string? orderManifest,
        [FromForm] List<IFormFile>? files,
        [FromForm] string? expectedRowVersion,
        [FromForm] bool deferModeration = false,
        CancellationToken cancellationToken = default)
    {
        var openedStreams = new List<Stream>();
        try
        {
            List<string> orderTokens;
            if (string.IsNullOrWhiteSpace(orderManifest))
            {
                orderTokens = [];
            }
            else
            {
                try
                {
                    orderTokens = JsonSerializer.Deserialize<List<string>>(orderManifest) ?? [];
                }
                catch (JsonException)
                {
                    return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        ["orderManifest"] = ["Thứ tự hình ảnh gửi lên không hợp lệ."]
                    }));
                }
            }

            byte[]? rowVersionBytes = null;
            if (!string.IsNullOrWhiteSpace(expectedRowVersion))
            {
                try
                {
                    rowVersionBytes = Convert.FromBase64String(expectedRowVersion);
                }
                catch (FormatException)
                {
                    return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        ["expectedRowVersion"] = ["RowVersion must be a valid Base64 string."]
                    }));
                }
            }

            var incomingFiles = files ?? [];
            if (incomingFiles.Count > MaxFilesPerRequest)
            {
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["files"] = [$"Chỉ được tải tối đa {MaxFilesPerRequest} hình ảnh mỗi lần."]
                }));
            }

            var (items, validationError) = await ReadAndValidateFilesAsync(incomingFiles, openedStreams, cancellationToken);
            if (validationError is not null)
            {
                return ValidationProblem(validationError);
            }

            var userId = GetRequiredUserId();
            var isAdmin = currentUser.IsInRole("Admin");
            var result = await listingImageService.SyncImagesAsync(
                listingId,
                userId,
                orderTokens,
                items,
                rowVersionBytes,
                deferModeration,
                isAdmin,
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
        finally
        {
            foreach (var stream in openedStreams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    [HttpPut("reorder")]
    [Authorize(Roles = "Seller, Admin")]
    public async Task<ActionResult<IReadOnlyList<ListingImageDto>>> ReorderImages(
        long listingId,
        [FromBody] ReorderListingImagesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.OrderedImageIds is null || request.OrderedImageIds.Count == 0)
            {
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["orderedImageIds"] = ["Danh sách thứ tự hình ảnh không được để trống."]
                }));
            }

            var userId = GetRequiredUserId();
            var isAdmin = currentUser.IsInRole("Admin");
            var reordered = await listingImageService.ReorderImagesAsync(
                listingId,
                userId,
                request.OrderedImageIds,
                isAdmin,
                cancellationToken);
            return Ok(reordered);
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ListingImageDto>>> GetImages(
        long listingId,
        CancellationToken cancellationToken)
    {
        try
        {
            var listing = await listingService.GetByIdAsync(listingId, cancellationToken);
            if (listing is null)
            {
                return NotFound();
            }

            var isOwnerOrStaff =
                (currentUser.UserId.HasValue && currentUser.UserId.Value == listing.OwnerUserId) ||
                currentUser.IsInRole("Admin") ||
                currentUser.IsInRole("Moderator");

            var isPubliclyVisible =
                listing.Status == ListingStatus.Published &&
                (!listing.EndDate.HasValue || listing.EndDate.Value > DateTimeOffset.UtcNow);

            if (!isPubliclyVisible && !isOwnerOrStaff)
            {
                return NotFound();
            }

            var images = await listingImageService.GetImagesByListingIdAsync(listingId, cancellationToken);
            return Ok(images);
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
    }

    [HttpPut("{imageId:long}")]
    [Authorize(Roles = "Seller, Admin")]
    public async Task<ActionResult<ListingImageDto>> UpdateImage(
        long listingId,
        long imageId,
        [FromBody] UpdateListingImageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetRequiredUserId();
            var isAdmin = currentUser.IsInRole("Admin");
            var updated = await listingImageService.UpdateImageAsync(listingId, imageId, userId, request, isAdmin, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
    }

    [HttpDelete("{imageId:long}")]
    [Authorize(Roles = "Seller, Admin")]
    public async Task<IActionResult> DeleteImage(
        long listingId,
        long imageId,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetRequiredUserId();
            var isAdmin = currentUser.IsInRole("Admin");
            await listingImageService.DeleteImageAsync(listingId, imageId, userId, isAdmin, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
    }

    private static async Task<(List<UploadImageItem> Items, ValidationProblemDetails? Error)> ReadAndValidateFilesAsync(
        List<IFormFile> files,
        List<Stream> openedStreams,
        CancellationToken cancellationToken)
    {
        var items = new List<UploadImageItem>(files.Count);
        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                return (items, new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    [file.FileName] = ["File cannot be empty."]
                }));
            }

            if (file.Length > MaxFileSizeInBytes)
            {
                return (items, new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    [file.FileName] = ["File size must not exceed 10 MB."]
                }));
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                return (items, new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    [file.FileName] = ["Only .jpg, .jpeg, .png, and .webp images are allowed."]
                }));
            }

            var memoryStream = new MemoryStream((int)Math.Min(file.Length, MaxFileSizeInBytes));
            openedStreams.Add(memoryStream);
            await file.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            if (!HasValidImageSignature(memoryStream))
            {
                return (items, new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    [file.FileName] = ["Tệp tải lên không phải là ảnh hợp lệ (JPEG/PNG/WEBP)."]
                }));
            }

            memoryStream.Position = 0;
            items.Add(new UploadImageItem(
                memoryStream,
                file.FileName,
                file.ContentType));
        }

        return (items, null);
    }

    private static bool HasValidImageSignature(Stream stream)
    {
        if (stream.Length < 12)
        {
            return false;
        }

        Span<byte> header = stackalloc byte[12];
        var read = stream.Read(header);
        if (read < 12)
        {
            return false;
        }

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return true;
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return true;
        }

        // WEBP: RIFF....WEBP
        if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            return true;
        }

        return false;
    }

    private long GetRequiredUserId() => currentUser.UserId
        ?? throw new UnauthorizedAccessException("A valid user identifier claim is required.");
}
