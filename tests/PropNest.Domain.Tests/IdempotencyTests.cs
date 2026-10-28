using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using PropNest.Api.Contracts.Listings;
using PropNest.Api.Controllers;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;
using PropNest.Application.Workflows;
using PropNest.Domain.Workflows;
using PropNest.Infrastructure.Persistence;
using PropNest.Infrastructure.Persistence.Repositories;

namespace PropNest.Domain.Tests;

public sealed class IdempotencyTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly PropNestDbContext _dbContext;
    private readonly WorkflowRepository _workflowRepository;

    public IdempotencyTests()
    {
        _sqliteConnection = new SqliteConnection("DataSource=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<PropNestDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new PropNestDbContext(options);
        _dbContext.Database.EnsureCreated();

        _workflowRepository = new WorkflowRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Dispose();
    }

    [Fact]
    public async Task NewKeyCreatesWorkflowPersistsEntitiesAndReturnsNewWorkflowDto()
    {
        // Arrange
        var service = new WorkflowService(_workflowRepository, _dbContext);
        const long userId = 100;
        const long listingId = 10;
        const string idempotencyKey = "NEW-KEY-001";
        const string requestPath = "/api/v1/listings/10/purchase-package";
        var requestHash = WorkflowService.ComputeRequestHash("payload-1");

        var command = new PurchasePackageCommand(
            listingId,
            userId,
            "VIP",
            150000m,
            idempotencyKey,
            requestPath,
            requestHash);

        // Act
        var result = await service.StartPurchaseAsync(command);

        // Assert: returned DTO
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.CorrelationId);
        Assert.Equal(listingId, result.ListingId);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("VIP", result.PackageCode);
        Assert.Equal(150000m, result.ChargeAmount);
        Assert.Equal(WorkflowStatus.Started, result.Status);
        Assert.Empty(result.Steps);

        // Assert: Database persistence (IdempotencyRequest)
        var persistedRequest = await _dbContext.IdempotencyRequests
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Key == idempotencyKey);
        Assert.NotNull(persistedRequest);
        Assert.Equal(result.CorrelationId, persistedRequest.CorrelationId);
        Assert.Equal(requestPath, persistedRequest.RequestPath);
        Assert.Equal(requestHash, persistedRequest.RequestHash);
        Assert.Equal("Pending", persistedRequest.Status);

        // Assert: Database persistence (WorkflowInstance)
        var persistedWorkflow = await _dbContext.WorkflowInstances
            .SingleOrDefaultAsync(x => x.CorrelationId == result.CorrelationId);
        Assert.NotNull(persistedWorkflow);
        Assert.Equal(WorkflowStatus.Started, persistedWorkflow.Status);
        Assert.Equal("VIP", persistedWorkflow.PackageCode);

        // Assert: Database persistence (OutboxMessage)
        var persistedOutbox = await _dbContext.OutboxMessages
            .SingleOrDefaultAsync(x => x.CorrelationId == result.CorrelationId);
        Assert.NotNull(persistedOutbox);
        Assert.Equal("PurchasePackageRequested", persistedOutbox.Type);
        Assert.Contains(result.CorrelationId.ToString(), persistedOutbox.PayloadJson);
    }

    [Fact]
    public async Task SameKeySameHashReplaysExistingWorkflowReturnsExistingCorrelationIdAndSteps()
    {
        // Arrange
        var service = new WorkflowService(_workflowRepository, _dbContext);
        const long userId = 100;
        const long listingId = 10;
        const string idempotencyKey = "REPLAY-KEY-002";
        const string requestPath = "/api/v1/listings/10/purchase-package";
        var requestHash = WorkflowService.ComputeRequestHash("same-payload");

        var initialCommand = new PurchasePackageCommand(
            listingId,
            userId,
            "VIP",
            150000m,
            idempotencyKey,
            requestPath,
            requestHash);

        // First call: initial creation
        var initialResult = await service.StartPurchaseAsync(initialCommand);

        // Simulate workflow progression: add a step
        var step = new WorkflowStep(initialResult.CorrelationId, "DeductWallet");
        step.Complete();
        await _workflowRepository.AddStepAsync(step);
        await _dbContext.SaveChangesAsync();

        var initialWorkflowCount = await _dbContext.WorkflowInstances.CountAsync();
        var initialOutboxCount = await _dbContext.OutboxMessages.CountAsync();
        var initialRequestCount = await _dbContext.IdempotencyRequests.CountAsync();

        // Act: Second call with exact same Key and RequestHash (Replay)
        var replayCommand = new PurchasePackageCommand(
            listingId,
            userId,
            "VIP",
            150000m,
            idempotencyKey,
            requestPath,
            requestHash);

        var replayResult = await service.StartPurchaseAsync(replayCommand);

        // Assert: Same CorrelationId and includes existing steps
        Assert.NotNull(replayResult);
        Assert.Equal(initialResult.CorrelationId, replayResult.CorrelationId);
        Assert.Single(replayResult.Steps);
        Assert.Equal("DeductWallet", replayResult.Steps[0].StepName);
        Assert.Equal("Completed", replayResult.Steps[0].Status);

        // Assert: No duplicate records created in database
        Assert.Equal(initialWorkflowCount, await _dbContext.WorkflowInstances.CountAsync());
        Assert.Equal(initialOutboxCount, await _dbContext.OutboxMessages.CountAsync());
        Assert.Equal(initialRequestCount, await _dbContext.IdempotencyRequests.CountAsync());
    }

    [Fact]
    public async Task SameKeyDifferentHashThrowsInvalidOperationExceptionPreventsTampering()
    {
        // Arrange
        var service = new WorkflowService(_workflowRepository, _dbContext);
        const long userId = 100;
        const long listingId = 10;
        const string idempotencyKey = "CONFLICT-KEY-003";
        const string requestPath = "/api/v1/listings/10/purchase-package";
        var originalHash = WorkflowService.ComputeRequestHash("original-payload-VIP-150k");
        var tamperedHash = WorkflowService.ComputeRequestHash("tampered-payload-Boost-30k");

        var initialCommand = new PurchasePackageCommand(
            listingId,
            userId,
            "VIP",
            150000m,
            idempotencyKey,
            requestPath,
            originalHash);

        // First call succeeds
        var initialResult = await service.StartPurchaseAsync(initialCommand);
        Assert.NotNull(initialResult);

        // Act & Assert: Second call with SAME key but DIFFERENT request payload/hash
        var tamperedCommand = new PurchasePackageCommand(
            listingId,
            userId,
            "Boost",
            30000m,
            idempotencyKey,
            requestPath,
            tamperedHash);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartPurchaseAsync(tamperedCommand));

        Assert.Equal("The Idempotency-Key has already been used with a different request.", exception.Message);

        // Verify database state was not modified
        var persistedWorkflow = await _dbContext.WorkflowInstances
            .SingleOrDefaultAsync(x => x.CorrelationId == initialResult.CorrelationId);
        Assert.NotNull(persistedWorkflow);
        Assert.Equal("VIP", persistedWorkflow.PackageCode);
        Assert.Equal(150000m, persistedWorkflow.ChargeAmount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingOrWhitespaceIdempotencyKeyThrowsArgumentException(string? invalidKey)
    {
        // Arrange
        var service = new WorkflowService(_workflowRepository, _dbContext);
        var command = new PurchasePackageCommand(
            ListingId: 10,
            UserId: 100,
            PackageCode: "VIP",
            ChargeAmount: 150000m,
            IdempotencyKey: invalidKey!,
            RequestPath: "/api/v1/listings/10/purchase-package",
            RequestHash: "SOME_HASH");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.StartPurchaseAsync(command));
        Assert.Contains("Idempotency-Key is required.", ex.Message);
    }

    [Fact]
    public async Task ExistingKeyWithoutAssociatedWorkflowThrowsInvalidOperationException()
    {
        // Arrange: Mock repository where IdempotencyRequest exists but GetByCorrelationIdAsync returns null
        var mockRepo = new Mock<IWorkflowRepository>();
        var mockUow = new Mock<IUnitOfWork>();

        const long userId = 100;
        const string key = "ORPHAN-KEY";
        const string hash = "HASH123";
        var correlationId = Guid.NewGuid();

        var existingRequest = new IdempotencyRequest(
            key,
            userId,
            "/path",
            hash,
            correlationId,
            DateTimeOffset.UtcNow.AddHours(24));

        mockRepo.Setup(r => r.GetIdempotencyRequestAsync(userId, key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRequest);

        mockRepo.Setup(r => r.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowInstance?)null);

        var service = new WorkflowService(mockRepo.Object, mockUow.Object);

        var command = new PurchasePackageCommand(10, userId, "VIP", 150000m, key, "/path", hash);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartPurchaseAsync(command));
        Assert.Equal("The idempotent request does not have an associated workflow.", ex.Message);
    }

    [Fact]
    public void ComputeRequestHashIsDeterministicAndSensitiveToPayload()
    {
        // Arrange
        const string payload1 = "{\"listingId\":10,\"packageCode\":\"VIP\",\"chargeAmount\":150000}";
        const string payload2 = "{\"listingId\":10,\"packageCode\":\"VIP\",\"chargeAmount\":150000}";
        const string payload3 = "{\"listingId\":10,\"packageCode\":\"Boost\",\"chargeAmount\":30000}";

        // Act
        var hash1 = WorkflowService.ComputeRequestHash(payload1);
        var hash2 = WorkflowService.ComputeRequestHash(payload2);
        var hash3 = WorkflowService.ComputeRequestHash(payload3);

        // Assert
        Assert.Equal(hash1, hash2); // Deterministic
        Assert.NotEqual(hash1, hash3); // Payload sensitive
        Assert.Equal(64, hash1.Length); // SHA-256 hex string (256 bits = 32 bytes = 64 hex chars)
        Assert.True(hash1.All(c => "0123456789ABCDEF".Contains(c)));
    }

    [Fact]
    public async Task DifferentUsersWithSameIdempotencyKeyAreIsolatedAndCreateSeparateWorkflows()
    {
        // Arrange
        var service = new WorkflowService(_workflowRepository, _dbContext);
        const string sharedKey = "SHARED-KEY-123";
        const string path = "/api/v1/listings/10/purchase-package";
        var hash = WorkflowService.ComputeRequestHash("payload");

        var user1Command = new PurchasePackageCommand(10, 100, "VIP", 150000m, sharedKey, path, hash);
        var user2Command = new PurchasePackageCommand(20, 200, "Standard", 50000m, sharedKey, path, hash);

        // Act
        var resultUser1 = await service.StartPurchaseAsync(user1Command);
        var resultUser2 = await service.StartPurchaseAsync(user2Command);

        // Assert
        Assert.NotEqual(resultUser1.CorrelationId, resultUser2.CorrelationId);
        Assert.Equal(100, resultUser1.UserId);
        Assert.Equal(200, resultUser2.UserId);

        var count = await _dbContext.IdempotencyRequests.CountAsync(x => x.Key == sharedKey);
        Assert.Equal(2, count);
    }

    [Fact]
    public void IdempotencyRequestDomainEntityCompleteUpdatesStatusAndResponse()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);
        var request = new IdempotencyRequest("KEY-1", 100, "/api/v1/purchase", "HASH", correlationId, expiresAt);

        // Verify initial state
        Assert.Equal("Pending", request.Status);
        Assert.Null(request.ResponseStatusCode);
        Assert.Null(request.ResponseBody);
        Assert.Equal(correlationId, request.CorrelationId);

        // Act
        const int statusCode = 202;
        const string responseBody = "{\"correlationId\":\"12345\"}";
        request.Complete(statusCode, responseBody);

        // Assert
        Assert.Equal("Completed", request.Status);
        Assert.Equal(statusCode, request.ResponseStatusCode);
        Assert.Equal(responseBody, request.ResponseBody);
        Assert.True(request.UpdatedAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task ListingsControllerPurchasePackageWithoutIdempotencyKeyReturnsValidationProblem()
    {
        // Arrange
        var mockCurrentUser = new Mock<ICurrentUser>();
        mockCurrentUser.Setup(u => u.UserId).Returns(100);

        var mockListingService = new Mock<IListingService>();
        var mockWorkflowService = new Mock<IWorkflowService>();

        var controller = new ListingsController(mockCurrentUser.Object, mockListingService.Object, mockWorkflowService.Object);

        var request = new PurchasePackageRequest { PackageCode = "VIP", ChargeAmount = 150000m };

        // Act
        var result = await controller.PurchasePackage(10, null, request, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problemDetails = Assert.IsAssignableFrom<ValidationProblemDetails>(objectResult.Value);
        Assert.True(problemDetails.Errors.ContainsKey("Idempotency-Key"));
        mockWorkflowService.Verify(s => s.StartPurchaseAsync(It.IsAny<PurchasePackageCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListingsControllerPurchasePackageWithValidKeyCallsWorkflowServiceAndReturnsAcceptedAtAction()
    {
        // Arrange
        var mockCurrentUser = new Mock<ICurrentUser>();
        mockCurrentUser.Setup(u => u.UserId).Returns(100);

        var mockListingService = new Mock<IListingService>();
        var mockWorkflowService = new Mock<IWorkflowService>();

        var correlationId = Guid.NewGuid();
        var expectedWorkflowDto = new WorkflowDto(
            correlationId,
            ListingId: 10,
            UserId: 100,
            PackageCode: "VIP",
            ChargeAmount: 150000m,
            Status: WorkflowStatus.Started,
            RetryCount: 0,
            ErrorMessage: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            Steps: Array.Empty<WorkflowStepDto>());

        mockWorkflowService
            .Setup(s => s.StartPurchaseAsync(It.IsAny<PurchasePackageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedWorkflowDto);

        var controller = new ListingsController(mockCurrentUser.Object, mockListingService.Object, mockWorkflowService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.ControllerContext.HttpContext.Request.Path = "/api/v1/listings/10/purchase-package";

        var request = new PurchasePackageRequest { PackageCode = "VIP", ChargeAmount = 150000m };

        // Act
        var result = await controller.PurchasePackage(10, "VALID-KEY-123", request, CancellationToken.None);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedAtActionResult>(result.Result);
        Assert.Equal(nameof(WorkflowsController.GetByCorrelationId), acceptedResult.ActionName);
        Assert.Equal("Workflows", acceptedResult.ControllerName);
        var returnedDto = Assert.IsType<WorkflowDto>(acceptedResult.Value);
        Assert.Equal(correlationId, returnedDto.CorrelationId);

        mockWorkflowService.Verify(s => s.StartPurchaseAsync(
            It.Is<PurchasePackageCommand>(c =>
                c.ListingId == 10 &&
                c.UserId == 100 &&
                c.PackageCode == "VIP" &&
                c.ChargeAmount == 150000m &&
                c.IdempotencyKey == "VALID-KEY-123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
