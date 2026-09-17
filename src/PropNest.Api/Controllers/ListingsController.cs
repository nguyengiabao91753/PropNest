using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropNest.Api.Contracts.Listings;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;
using PropNest.Application.Workflows;

namespace PropNest.Api.Controllers;

[ApiController]
[Authorize(Roles = "Seller")]
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

    [HttpPost]
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
    public async Task<ActionResult<ListingDto>> Update(long listingId, UpdateListingRequest request, CancellationToken cancellationToken)
    {
        byte[] rowVersion;
        try
        {
            rowVersion = Convert.FromBase64String(request.RowVersion);
        }
        catch (FormatException)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(request.RowVersion)] = ["RowVersion must be base64 encoded."] }));
        }

        var listing = await listingService.UpdateAsync(new UpdateListingCommand(
            listingId,
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
            rowVersion), cancellationToken);

        return Ok(listing);
    }

    [HttpPost("{listingId:long}/submit")]
    public async Task<IActionResult> Submit(long listingId, CancellationToken cancellationToken)
    {
        await listingService.SubmitAsync(listingId, GetRequiredUserId(), cancellationToken);
        return Accepted();
    }

    [HttpPost("{listingId:long}/purchase-package")]
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

    private long GetRequiredUserId() => currentUser.UserId
        ?? throw new UnauthorizedAccessException("A valid user identifier claim is required.");
}
