using Microsoft.AspNetCore.Mvc;
using Moq;
using PropNest.Api.Controllers;
using PropNest.Application.Abstractions;
using PropNest.Application.Workflows;
using PropNest.Domain.Workflows;

namespace PropNest.Domain.Tests;

public sealed class WorkflowsControllerTests
{
    private readonly Mock<ICurrentUser> _mockCurrentUser = new();
    private readonly Mock<IWorkflowService> _mockWorkflowService = new();
    private readonly WorkflowsController _controller;

    public WorkflowsControllerTests()
    {
        _controller = new WorkflowsController(_mockCurrentUser.Object, _mockWorkflowService.Object);
    }

    [Fact]
    public async Task GetByCorrelationIdReturnsNotFoundWhenWorkflowDoesNotExist()
    {
        var correlationId = Guid.NewGuid();
        _mockWorkflowService
            .Setup(s => s.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowDto?)null);

        var result = await _controller.GetByCorrelationId(correlationId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetByCorrelationIdReturnsUnauthorizedWhenUserIdClaimIsMissing()
    {
        var correlationId = Guid.NewGuid();
        var workflowDto = CreateSampleWorkflowDto(correlationId, userId: 100);

        _mockWorkflowService
            .Setup(s => s.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflowDto);

        _mockCurrentUser.Setup(u => u.UserId).Returns((long?)null);

        var result = await _controller.GetByCorrelationId(correlationId, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetByCorrelationIdReturnsForbidWhenUserIsNotOwnerAndNotAdmin()
    {
        var correlationId = Guid.NewGuid();
        var workflowDto = CreateSampleWorkflowDto(correlationId, userId: 100);

        _mockWorkflowService
            .Setup(s => s.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflowDto);

        _mockCurrentUser.Setup(u => u.UserId).Returns(200);
        _mockCurrentUser.Setup(u => u.IsInRole("Admin")).Returns(false);

        var result = await _controller.GetByCorrelationId(correlationId, CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetByCorrelationIdReturnsOkWhenUserIsOwner()
    {
        var correlationId = Guid.NewGuid();
        var workflowDto = CreateSampleWorkflowDto(correlationId, userId: 100);

        _mockWorkflowService
            .Setup(s => s.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflowDto);

        _mockCurrentUser.Setup(u => u.UserId).Returns(100);
        _mockCurrentUser.Setup(u => u.IsInRole("Admin")).Returns(false);

        var result = await _controller.GetByCorrelationId(correlationId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<WorkflowDto>(okResult.Value);
        Assert.Equal(correlationId, returned.CorrelationId);
        Assert.Equal(100, returned.UserId);
        Assert.Equal(2, returned.Steps.Count);
        Assert.Equal("DeductWallet", returned.Steps[0].StepName);
        Assert.Equal("Completed", returned.Steps[0].Status);
        Assert.NotNull(returned.Steps[0].FinishedAt);
        Assert.Equal("UpgradeListing", returned.Steps[1].StepName);
        Assert.Equal("Started", returned.Steps[1].Status);
        Assert.Null(returned.Steps[1].FinishedAt);
    }

    [Fact]
    public async Task GetByCorrelationIdReturnsOkWhenUserIsAdminEvenIfNotOwner()
    {
        var correlationId = Guid.NewGuid();
        var workflowDto = CreateSampleWorkflowDto(correlationId, userId: 100);

        _mockWorkflowService
            .Setup(s => s.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflowDto);

        _mockCurrentUser.Setup(u => u.UserId).Returns(999);
        _mockCurrentUser.Setup(u => u.IsInRole("Admin")).Returns(true);

        var result = await _controller.GetByCorrelationId(correlationId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<WorkflowDto>(okResult.Value);
        Assert.Equal(correlationId, returned.CorrelationId);
    }

    [Fact]
    public async Task GetByCorrelationIdIncludesFailedStepWithError()
    {
        var correlationId = Guid.NewGuid();
        const string reason = "Insufficient balance";
        var workflowDto = new WorkflowDto(
            correlationId,
            ListingId: 10,
            UserId: 100,
            PackageCode: "VIP",
            ChargeAmount: 150000m,
            Status: WorkflowStatus.Failed,
            RetryCount: 0,
            ErrorMessage: reason,
            CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-1),
            UpdatedAt: DateTimeOffset.UtcNow,
            Steps: new List<WorkflowStepDto>
            {
                new("DeductWallet", "Failed", 1, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow, reason)
            });

        _mockWorkflowService
            .Setup(s => s.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflowDto);

        _mockCurrentUser.Setup(u => u.UserId).Returns(100);
        _mockCurrentUser.Setup(u => u.IsInRole("Admin")).Returns(false);

        var result = await _controller.GetByCorrelationId(correlationId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<WorkflowDto>(okResult.Value);
        Assert.Single(returned.Steps);
        Assert.Equal("Failed", returned.Steps[0].Status);
        Assert.Equal(reason, returned.Steps[0].Error);
        Assert.NotNull(returned.Steps[0].FinishedAt);
    }

    private static WorkflowDto CreateSampleWorkflowDto(Guid correlationId, long userId)
    {
        var now = DateTimeOffset.UtcNow;
        var steps = new List<WorkflowStepDto>
        {
            new("DeductWallet", "Completed", 1, now.AddSeconds(-10), now.AddSeconds(-5), null),
            new("UpgradeListing", "Started", 1, now.AddSeconds(-4), null, null)
        };

        return new WorkflowDto(
            correlationId,
            ListingId: 10,
            UserId: userId,
            PackageCode: "VIP",
            ChargeAmount: 150000m,
            Status: WorkflowStatus.UpgradingListing,
            RetryCount: 0,
            ErrorMessage: null,
            CreatedAt: now.AddSeconds(-10),
            UpdatedAt: now.AddSeconds(-4),
            Steps: steps);
    }
}
