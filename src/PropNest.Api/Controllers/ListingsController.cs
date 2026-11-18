using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropNest.Api.Contracts.Listings;
using PropNest.Api.Extensions;
using PropNest.Application.Abstractions;
using PropNest.Application.Common;
using PropNest.Application.Listings;
using PropNest.Application.Workflows;

namespace PropNest.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/listings")]
public sealed class ListingsController(
    ICurrentUser currentUser,
    IListingService listingService,
    IWorkflowService workflowService) : ControllerBase
{
    [HttpGet("{listingId:long}")]
    [AllowAnonymous]
    public async Task<ActionResult<ListingDto>> GetById(long listingId, CancellationToken cancellationToken)
    {
        var listing = await listingService.GetByIdAsync(listingId, cancellationToken);
        return listing is null ? NotFound() : Ok(listing);
    }

    [HttpGet("mine")]
    [Authorize(Roles = "Seller, Admin")]
    public async Task<ActionResult<ListingCollectionDto>> GetMyListings(CancellationToken cancellationToken)
    {
        return Ok(await listingService.GetByOwnerAsync(
            GetRequiredUserId(),
            cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = "Seller, Admin")]
    public async Task<ActionResult<ListingDto>> Create(CreateListingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var listing = await listingService.CreateAsync(new CreateListingCommand(
                GetRequiredUserId(),
                request.Title,
                request.Description,
                request.PropertyType,
                request.ListingType,
                request.Price,
                request.Area,
                request.City,
                request.District,
                request.Ward,
                request.Address,
                request.Floors,
                request.Bedrooms,
                request.Bathrooms,
                request.BalconyDirection,
                request.AdministrativeMode,
                request.ProvinceCode,
                request.DistrictCode,
                request.WardCode,
                request.LegacyAddress), cancellationToken);

            return CreatedAtAction(nameof(GetById), new { listingId = listing.ListingId }, listing);
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
    }

    [HttpPost("suggest-description")]
    [Authorize(Roles = "Seller, Admin")]
    public async Task<ActionResult<ListingDescriptionSuggestionResult>> SuggestDescription(
        [FromBody] SuggestListingDescriptionRequest request,
        [FromServices] IListingDescriptionGenerator descriptionGenerator,
        CancellationToken cancellationToken)
    {
        var result = await descriptionGenerator.GenerateDescriptionAsync(new SuggestListingDescriptionCommand(
            request.Title,
            request.PropertyType,
            request.ListingType,
            request.Price,
            request.Area,
            request.City,
            request.District,
            request.Ward,
            request.Address,
            request.LegacyAddress,
            request.AdministrativeMode,
            request.Floors,
            request.Bedrooms,
            request.Bathrooms,
            request.BalconyDirection), cancellationToken);

        return Ok(result);
    }

    [HttpPut("{listingId:long}")]
    [Authorize(Roles = "Seller, Admin")]
    public async Task<ActionResult<ListingDto>> Update(long listingId, UpdateListingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var rowVersion = Convert.FromBase64String(request.RowVersion);

            var listing = await listingService.GetByIdAsync(listingId, cancellationToken)
                ?? throw new KeyNotFoundException($"Listing with ID {listingId} not found.");
            currentUser.EnsureOwner(listing.OwnerUserId);

            var updatedListing = await listingService.UpdateAsync(new UpdateListingCommand(
                listingId,
                listing.OwnerUserId,
                request.Title,
                request.Description,
                request.PropertyType,
                request.ListingType,
                request.Price,
                request.Area,
                request.City,
                request.District,
                request.Ward,
                request.Address,
                rowVersion,
                request.Floors,
                request.Bedrooms,
                request.Bathrooms,
                request.BalconyDirection,
                request.AdministrativeMode,
                request.ProvinceCode,
                request.DistrictCode,
                request.WardCode,
                request.LegacyAddress), cancellationToken);
            return Ok(updatedListing);
        }
        catch (FormatException)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.RowVersion)] = ["RowVersion must be base64 encoded."]
            }));
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
    }

    [HttpDelete("{listingId:long}")]
    [Authorize(Roles = "Seller, Admin")]
    public async Task<IActionResult> Delete(long listingId, CancellationToken cancellationToken)
    {
        try
        {
            var listing = await listingService.GetByIdAsync(listingId, cancellationToken) ?? throw new KeyNotFoundException($"Listing with ID {listingId} not found.");
            currentUser.EnsureOwner(listing.OwnerUserId);
            await listingService.DeleteAsync(listingId, listing.OwnerUserId, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
    }

    [HttpPost("{listingId:long}/submit")]
    [Authorize(Roles = "Seller, Admin")]
    public async Task<IActionResult> Submit(long listingId, CancellationToken cancellationToken)
    {
        try
        {
            var listing = await listingService.GetByIdAsync(listingId, cancellationToken) ?? throw new KeyNotFoundException($"Listing with ID {listingId} not found.");
            currentUser.EnsureOwner(listing.OwnerUserId);

            await listingService.SubmitAsync(listingId, listing.OwnerUserId, cancellationToken);
            return Accepted();
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
    }

    [HttpGet("~/api/v1/packages")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PackageDto>>> GetPackages(CancellationToken cancellationToken)
    {
        var packages = await listingService.GetActivePackagesAsync(cancellationToken);
        return Ok(packages);
    }

    [HttpPost("{listingId:long}/purchase-package")]
    [Authorize(Roles = "Seller, Admin")]
    public async Task<ActionResult<WorkflowDto>> PurchasePackage(
        long listingId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        PurchasePackageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["The Idempotency-Key header is required."] }));
        }

        if (string.IsNullOrWhiteSpace(request.PackageCode))
        {
            return Problem(
                detail: "Vui lòng chọn gói dịch vụ hợp lệ.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        try
        {
            var listing = await listingService.GetByIdAsync(listingId, cancellationToken)
                ?? throw new KeyNotFoundException($"Listing with ID {listingId} not found.");
            currentUser.EnsureOwner(listing.OwnerUserId);

            if (!PropNest.Domain.Listings.ListingPackagePolicy.CanPurchasePackage(listing.Status, listing.ModerationDecision))
            {
                return Problem(
                    detail: "Chỉ tin đăng đã được duyệt (Chờ thanh toán, Đang hiển thị hoặc Hết hạn) mới có thể mua hoặc gia hạn gói.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request");
            }

            var package = await listingService.GetPackageByCodeAsync(request.PackageCode.Trim(), cancellationToken);
            if (package is null || !PropNest.Domain.Listings.ListingPackagePolicy.IsPurchasablePackage(package.Kind, package.Price, package.IsActive))
            {
                return Problem(
                    detail: $"Gói dịch vụ '{request.PackageCode}' không tồn tại hoặc đã ngừng áp dụng.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request");
            }

            var userId = GetRequiredUserId();
            var requestHash = WorkflowService.ComputeRequestHash(JsonSerializer.Serialize(new { listingId, request }));
            var workflow = await workflowService.StartPurchaseAsync(new PurchasePackageCommand(
                listingId,
                userId,
                package.PackageCode,
                package.Price,
                idempotencyKey,
                Request.Path,
                requestHash), cancellationToken);

            return AcceptedAtAction(nameof(WorkflowsController.GetByCorrelationId), "Workflows", new { correlationId = workflow.CorrelationId }, workflow);
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ListingSummaryDto>>> GetPublicListings([FromQuery] GetPublicListingsQuery request, CancellationToken cancellationToken)
    {
        var result = await listingService.GetPublicListingsAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{listingId:long}/hide")]
    [Authorize(Roles = "Seller, Admin")]
    public async Task<IActionResult> Hide(long listingId, CancellationToken cancellationToken)
    {
        try
        {
            var listing = await listingService.GetByIdAsync(listingId, cancellationToken) ?? throw new KeyNotFoundException($"Listing with ID {listingId} not found.");
            currentUser.EnsureOwner(listing.OwnerUserId);

            await listingService.HideAsync(listingId, listing.OwnerUserId, cancellationToken);
            return Ok(new { message = "Listing has been hidden successfully." });
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
    }

    [HttpPost("{listingId:long}/unhide")]
    [Authorize(Roles = "Seller, Admin")]
    public async Task<IActionResult> Unhide(long listingId, CancellationToken cancellationToken)
    {
        try
        {
            var listing = await listingService.GetByIdAsync(listingId, cancellationToken) ?? throw new KeyNotFoundException($"Listing with ID {listingId} not found.");
            currentUser.EnsureOwner(listing.OwnerUserId);

            var updated = await listingService.UnhideAsync(listingId, listing.OwnerUserId, cancellationToken);
            return Ok(new { message = "Listing visibility has been restored.", status = updated.Status.ToString() });
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
    }

    [HttpGet("{listingId:long}/history")]
    [Authorize(Roles = "Seller, Moderator, Admin")]
    public async Task<ActionResult<IReadOnlyList<ListingHistoryDto>>> GetHistories(long listingId, CancellationToken cancellationToken)
    {
        try
        {
            var listing = await listingService.GetByIdAsync(listingId, cancellationToken) ?? throw new KeyNotFoundException($"Listing with ID {listingId} not found.");
            currentUser.EnsureCanViewHistory(listing.OwnerUserId);

            var histories = await listingService.GetHistoriesAsync(listingId, cancellationToken);
            return Ok(histories);
        }
        catch (Exception ex)
        {
            return this.HandleException(ex);
        }
    }

    private long GetRequiredUserId() => currentUser.UserId
        ?? throw new UnauthorizedAccessException("A valid user identifier claim is required.");
}
