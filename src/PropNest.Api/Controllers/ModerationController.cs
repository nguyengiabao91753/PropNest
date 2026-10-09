using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;
using PropNest.Api.Contracts.Moderation;
using System.Text.Json;

namespace PropNest.Api.Controllers;

[ApiController]
[Authorize(Policy = "CanModerateListings")]
[Route("api/v1/moderation")]
public sealed class ModerationController(
    ICurrentUser currentUser,
    IListingService listingService) : ControllerBase
{
    [HttpGet("listings/pending")]
    public async Task<ActionResult<ListingCollectionDto>> GetPendingListings(CancellationToken cancellationToken)
    {
        return Ok(await listingService.GetPendingModerationAsync(cancellationToken));
    }

    [HttpPost("listings/{listingId:long}/approve")]
    public async Task<ActionResult<ListingDto>> Approve(
        long listingId,
        CancellationToken cancellationToken)
    {
        var moderatorUserId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("A valid user identifier claim is required.");

        var listing = await listingService.ApproveAsync(
            listingId,
            moderatorUserId,
            cancellationToken);

        return Ok(listing);
    }

    [HttpPost("listings/{listingId:long}/reject")]
    public async Task<ActionResult<ListingDto>> Reject(
        long listingId,
        RejectListingRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ReasonsJson))
        {
            return BadRequest(new { message = "reasonsJson is required." });
        }

        try
        {
            using var reasonsDocument = JsonDocument.Parse(request.ReasonsJson);
            if (reasonsDocument.RootElement.ValueKind != JsonValueKind.Array)
            {
                return BadRequest(new { message = "reasonsJson must be a JSON array." });
            }
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "reasonsJson must be valid JSON." });
        }

        var moderatorUserId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("A valid user identifier claim is required.");

        var listing = await listingService.RejectAsync(
            listingId,
            moderatorUserId,
            request.ReasonsJson,
            cancellationToken);

        return Ok(listing);
    }
}