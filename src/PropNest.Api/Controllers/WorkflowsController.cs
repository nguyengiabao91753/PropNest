using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropNest.Application.Abstractions;
using PropNest.Application.Workflows;

namespace PropNest.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/workflows")]
public sealed class WorkflowsController(ICurrentUser currentUser, IWorkflowService workflowService) : ControllerBase
{
    [HttpGet("{correlationId:guid}")]
    public async Task<ActionResult<WorkflowDto>> GetByCorrelationId(Guid correlationId, CancellationToken cancellationToken)
    {
        var workflow = await workflowService.GetByCorrelationIdAsync(correlationId, cancellationToken);
        if (workflow is null)
        {
            return NotFound();
        }

        var userId = currentUser.UserId;
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        if (workflow.UserId != userId.Value && !currentUser.IsInRole("Admin"))
        {
            return Forbid();
        }

        return Ok(workflow);
    }
}
