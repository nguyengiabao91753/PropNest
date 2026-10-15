using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropNest.Api.Contracts.Listings;
using PropNest.Api.Extensions;
using PropNest.Application.Abstractions;
using PropNest.Application.Common;
using PropNest.Application.Listings;
using PropNest.Application.Workflows;
using System.Text.Json;

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
    public async Task<ActionResult<ListingDto>> GetById(long listingId, CancellationToken cancellationToken)
    {
        var listing = await listingService.GetByIdAsync(listingId, cancellationToken);
        return listing is null ? NotFound() : Ok(listing);
    }

    [HttpGet("mine")]
    [Authorize(Roles = "Seller")]
    public async Task<ActionResult<ListingCollectionDto>> GetMyListings(CancellationToken cancellationToken)
    {
        return Ok(await listingService.GetByOwnerAsync(
            GetRequiredUserId(),
            cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles ="Seller")]
    public async Task<ActionResult<ListingDto>> Create(CreateListingRequest request, CancellationToken cancellationToken)
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
            request.Address), cancellationToken);

        return CreatedAtAction(nameof(GetById), new { listingId = listing.ListingId }, listing);
    }

    [HttpPut("{listingId:long}")]
    [Authorize(Roles ="Seller")]
    public async Task<ActionResult<ListingDto>> Update(long listingId, UpdateListingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            byte[] rowVersion;
            rowVersion = Convert.FromBase64String(request.RowVersion);
            
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
                rowVersion), cancellationToken);
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

    [HttpPost("{listingId:long}/submit")]
    [Authorize(Roles ="Seller")]
    public async Task<IActionResult> Submit(long listingId, CancellationToken cancellationToken)
    {
        try
        {
            var listing = await listingService.GetByIdAsync(listingId, cancellationToken) ?? throw new KeyNotFoundException($"Listing with ID {listingId} not found.");
            currentUser.EnsureOwner(listing.OwnerUserId);

            await listingService.SubmitAsync(listingId, listing.OwnerUserId, cancellationToken);
            return Accepted();
        } catch(Exception ex)
        {
            return this.HandleException(ex);
        }
    }

    [HttpPost("{listingId:long}/purchase-package")]
    [Authorize(Roles = "Seller")]
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

        var userId = GetRequiredUserId();
        var requestHash = WorkflowService.ComputeRequestHash(JsonSerializer.Serialize(new { listingId, request }));
        var workflow = await workflowService.StartPurchaseAsync(new PurchasePackageCommand(
            listingId,
            userId,
            request.PackageCode,
            request.ChargeAmount,
            idempotencyKey,
            Request.Path,
            requestHash), cancellationToken);

        return AcceptedAtAction(nameof(WorkflowsController.GetByCorrelationId), "Workflows", new { correlationId = workflow.CorrelationId }, workflow);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ListingSummaryDto>>> GetPublicListings([FromQuery] GetPublicListingsQuery request, CancellationToken cancellationToken)
    {
        var result = await listingService.GetPublicListingsAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{listingId:long}/hide")]
    [Authorize(Roles = "Seller")]
    public async Task<IActionResult> Hide(long listingId, CancellationToken cancellationToken)
    {
        try
        {
            var listing = await listingService.GetByIdAsync(listingId, cancellationToken) ?? throw new KeyNotFoundException($"Listing with ID {listingId} not found.");
            currentUser.EnsureOwner(listing.OwnerUserId);


            await listingService.HideAsync(listingId, listing.OwnerUserId, cancellationToken);
            return Ok(new { message = "Listing has been hidden successfully." });
        } catch(Exception ex)
        {
            return this.HandleException(ex);
        }
    }
        

    [HttpGet("{listingId:long}/history")]
    [Authorize(Roles ="Seller, Moderator, Admin")]
    public async Task<ActionResult<IReadOnlyList<ListingHistoryDto>>> GetHistories(long listingId, CancellationToken cancellationToken)
    {
        try
        {
            var listing = await listingService.GetByIdAsync(listingId, cancellationToken) ?? throw new KeyNotFoundException($"Listing with ID {listingId} not found.");
            currentUser.EnsureCanViewHistory(listing.OwnerUserId);

            var histories = await listingService.GetHistoriesAsync(listingId, cancellationToken);
            return Ok(histories);
        } catch(Exception ex)
        {
            return this.HandleException(ex);
        }
    }
        

    private long GetRequiredUserId() => currentUser.UserId
        ?? throw new UnauthorizedAccessException("A valid user identifier claim is required.");
}
