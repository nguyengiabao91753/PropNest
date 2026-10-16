using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropNest.Api.Extensions;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;

namespace PropNest.Api.Controllers;

[ApiController]
[Route("api/v1/listings/{listingId:long}/images")]
public sealed class ListingImagesController(
    ICurrentUser currentUser,
    IListingImageService listingImageService) : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private const long MaxFileSizeInBytes = 10 * 1024 * 1024; // 10 MB

    [HttpPost]
    [Authorize(Roles = "Seller")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<IReadOnlyList<ListingImageDto>>> UploadImages(
        long listingId,
        [FromForm] List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        try
        {
            if (files is null || files.Count == 0)
            {
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["files"] = ["At least one image file is required."]
                }));
            }

            var items = new List<UploadImageItem>();
            foreach (var file in files)
            {
                if (file.Length == 0)
                {
                    return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        [file.FileName] = ["File cannot be empty."]
                    }));
                }

                if (file.Length > MaxFileSizeInBytes)
                {
                    return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        [file.FileName] = ["File size must not exceed 10 MB."]
                    }));
                }

                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
                {
                    return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        [file.FileName] = ["Only .jpg, .jpeg, .png, and .webp images are allowed."]
                    }));
                }

                items.Add(new UploadImageItem(
                    file.OpenReadStream(),
                    file.FileName,
                    file.ContentType));
            }

            var userId = GetRequiredUserId();
            var images = await listingImageService.UploadImagesAsync(listingId, userId, items, cancellationToken);
            return CreatedAtAction(nameof(GetImages), new { listingId }, images);
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
            var images = await listingImageService.GetImagesByListingIdAsync(listingId, cancellationToken);
            return Ok(images);
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
    }

    [HttpPut("{imageId:long}")]
    [Authorize(Roles = "Seller")]
    public async Task<ActionResult<ListingImageDto>> UpdateImage(
        long listingId,
        long imageId,
        [FromBody] UpdateListingImageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetRequiredUserId();
            var updated = await listingImageService.UpdateImageAsync(listingId, imageId, userId, request, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
    }

    [HttpDelete("{imageId:long}")]
    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> DeleteImage(
        long listingId,
        long imageId,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetRequiredUserId();
            await listingImageService.DeleteImageAsync(listingId, imageId, userId, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
    }

    private long GetRequiredUserId() => currentUser.UserId
        ?? throw new UnauthorizedAccessException("A valid user identifier claim is required.");
}
