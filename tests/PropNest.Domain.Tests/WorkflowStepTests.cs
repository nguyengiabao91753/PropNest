using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PropNest.Application.Workflows;
using PropNest.Domain.Workflows;
using PropNest.Infrastructure.Persistence;
using PropNest.Infrastructure.Persistence.Repositories;
using PropNest.Infrastructure.Workflows;

namespace PropNest.Domain.Tests;

public sealed class WorkflowStepTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly PropNestDbContext _dbContext;

    public WorkflowStepTests()
    {
        _sqliteConnection = new SqliteConnection("DataSource=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<PropNestDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new PropNestDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Dispose();
    }

    [Fact]
    public void NewWorkflowStepHasStartedStatusAndCorrectAttempt()
    {
        var correlationId = Guid.NewGuid();
        var step = new WorkflowStep(correlationId, "DeductWallet");

        Assert.Equal(correlationId, step.CorrelationId);
        Assert.Equal("DeductWallet", step.StepName);
        Assert.Equal("Started", step.Status);
        Assert.Equal(1, step.Attempt);
        Assert.True(step.StartedAt <= DateTimeOffset.UtcNow);
        Assert.Null(step.FinishedAt);
        Assert.Null(step.Error);
    }

    [Fact]
    public void NewWorkflowStepWithCustomAttemptSetsSpecifiedAttempt()
    {
        var correlationId = Guid.NewGuid();
        var step = new WorkflowStep(correlationId, "UpgradeListing", attempt: 3);

        Assert.Equal(3, step.Attempt);
        Assert.Equal("Started", step.Status);
    }

    [Fact]
    public void NewWorkflowStepWithZeroOrNegativeAttemptDefaultsToOne()
    {
        var correlationId = Guid.NewGuid();
        var stepZero = new WorkflowStep(correlationId, "DeductWallet", attempt: 0);
        var stepNegative = new WorkflowStep(correlationId, "DeductWallet", attempt: -2);

        Assert.Equal(1, stepZero.Attempt);
        Assert.Equal(1, stepNegative.Attempt);
    }

    [Fact]
    public void CompleteSetsStatusToCompletedAndFinishedAt()
    {
        var correlationId = Guid.NewGuid();
        var step = new WorkflowStep(correlationId, "DeductWallet");

        step.Complete();

        Assert.Equal("Completed", step.Status);
        Assert.NotNull(step.FinishedAt);
        Assert.True(step.FinishedAt >= step.StartedAt);
        Assert.Null(step.Error);
    }

    [Fact]
    public void FailSetsStatusToFailedErrorAndFinishedAt()
    {
        var correlationId = Guid.NewGuid();
        var step = new WorkflowStep(correlationId, "DeductWallet");
        const string errorMessage = "Insufficient balance";

        step.Fail(errorMessage);

        Assert.Equal("Failed", step.Status);
        Assert.Equal(errorMessage, step.Error);
        Assert.NotNull(step.FinishedAt);
        Assert.True(step.FinishedAt >= step.StartedAt);
        Assert.Equal(1, step.Attempt);
    }

    [Fact]
    public async Task WorkflowRepositoryGetStepsByCorrelationIdAsyncReturnsChronologicalSteps()
    {
        var correlationId = Guid.NewGuid();
        var workflow = new WorkflowInstance(correlationId, 100, 200, "VIP", 150000m);
        await _dbContext.WorkflowInstances.AddAsync(workflow);

        var repo = new WorkflowRepository(_dbContext);

        var step1 = new WorkflowStep(correlationId, "DeductWallet");
        step1.Complete();
        await repo.AddStepAsync(step1);

        var step2 = new WorkflowStep(correlationId, "UpgradeListing");
        step2.Complete();
        await repo.AddStepAsync(step2);

        var otherCorrelationId = Guid.NewGuid();
        var otherWorkflow = new WorkflowInstance(otherCorrelationId, 101, 201, "Standard", 50000m);
        await _dbContext.WorkflowInstances.AddAsync(otherWorkflow);
        var stepOther = new WorkflowStep(otherCorrelationId, "DeductWallet");
        await repo.AddStepAsync(stepOther);

        await _dbContext.SaveChangesAsync();

        var steps = await repo.GetStepsByCorrelationIdAsync(correlationId);

        Assert.Equal(2, steps.Count);
        Assert.Equal("DeductWallet", steps[0].StepName);
        Assert.Equal("Completed", steps[0].Status);
        Assert.Equal("UpgradeListing", steps[1].StepName);
        Assert.Equal("Completed", steps[1].Status);
        Assert.True(steps[0].StartedAt <= steps[1].StartedAt);
    }

    [Fact]
    public async Task WorkflowRepositoryGetStepsByCorrelationIdAsyncReturnsEmptyWhenNoneFound()
    {
        var repo = new WorkflowRepository(_dbContext);
        var steps = await repo.GetStepsByCorrelationIdAsync(Guid.NewGuid());

        Assert.Empty(steps);
    }

    [Fact]
    public async Task StepLifecycleHappyPathTransitionsCorrectlyWithoutDuplicates()
    {
        var correlationId = Guid.NewGuid();
        var workflow = new WorkflowInstance(correlationId, 100, 200, "VIP", 150000m);
        await _dbContext.WorkflowInstances.AddAsync(workflow);
        var repo = new WorkflowRepository(_dbContext);

        // Step 1: Start DeductWallet
        workflow.MoveTo(WorkflowStatus.DeductingWallet);
        var deductStep = new WorkflowStep(correlationId, "DeductWallet");
        await repo.AddStepAsync(deductStep);
        await _dbContext.SaveChangesAsync();

        var stepsAfterStart = await repo.GetStepsByCorrelationIdAsync(correlationId);
        Assert.Single(stepsAfterStart);
        Assert.Equal("DeductWallet", stepsAfterStart[0].StepName);
        Assert.Equal("Started", stepsAfterStart[0].Status);
        Assert.Null(stepsAfterStart[0].FinishedAt);

        // Step 2: Complete DeductWallet, Start UpgradeListing
        workflow.MoveTo(WorkflowStatus.UpgradingListing);
        deductStep.Complete();
        var upgradeStep = new WorkflowStep(correlationId, "UpgradeListing");
        await repo.AddStepAsync(upgradeStep);
        await _dbContext.SaveChangesAsync();

        var stepsAfterDeducted = await repo.GetStepsByCorrelationIdAsync(correlationId);
        Assert.Equal(2, stepsAfterDeducted.Count);
        Assert.Equal("DeductWallet", stepsAfterDeducted[0].StepName);
        Assert.Equal("Completed", stepsAfterDeducted[0].Status);
        Assert.NotNull(stepsAfterDeducted[0].FinishedAt);
        Assert.Equal("UpgradeListing", stepsAfterDeducted[1].StepName);
        Assert.Equal("Started", stepsAfterDeducted[1].Status);
        Assert.Null(stepsAfterDeducted[1].FinishedAt);

        // Step 3: Complete UpgradeListing, Complete RecordHistory
        workflow.MoveTo(WorkflowStatus.Completed);
        upgradeStep.Complete();
        var historyStep = new WorkflowStep(correlationId, "RecordHistory");
        historyStep.Complete();
        await repo.AddStepAsync(historyStep);
        await _dbContext.SaveChangesAsync();

        var stepsAfterCompleted = await repo.GetStepsByCorrelationIdAsync(correlationId);
        Assert.Equal(3, stepsAfterCompleted.Count);
        Assert.All(stepsAfterCompleted, s => Assert.Equal("Completed", s.Status));
        Assert.All(stepsAfterCompleted, s => Assert.NotNull(s.FinishedAt));
        Assert.Equal(WorkflowStatus.Completed, workflow.Status);
    }

    [Fact]
    public async Task StepLifecycleDeductWalletFailureUpdatesStepInPlace()
    {
        var correlationId = Guid.NewGuid();
        var workflow = new WorkflowInstance(correlationId, 100, 200, "VIP", 150000m);
        await _dbContext.WorkflowInstances.AddAsync(workflow);
        var repo = new WorkflowRepository(_dbContext);

        // Start DeductWallet
        workflow.MoveTo(WorkflowStatus.DeductingWallet);
        var deductStep = new WorkflowStep(correlationId, "DeductWallet");
        await repo.AddStepAsync(deductStep);
        await _dbContext.SaveChangesAsync();

        // Fail DeductWallet
        const string reason = "Insufficient balance in wallet";
        workflow.MoveTo(WorkflowStatus.Failed, reason);
        deductStep.Fail(reason);
        await _dbContext.SaveChangesAsync();

        var steps = await repo.GetStepsByCorrelationIdAsync(correlationId);
        Assert.Single(steps);
        Assert.Equal("DeductWallet", steps[0].StepName);
        Assert.Equal("Failed", steps[0].Status);
        Assert.Equal(reason, steps[0].Error);
        Assert.NotNull(steps[0].FinishedAt);
        Assert.Equal(WorkflowStatus.Failed, workflow.Status);
        Assert.Equal(reason, workflow.ErrorMessage);
    }

    [Fact]
    public async Task StepLifecycleUpgradeListingFailureAndCompensationTracksStepsCorrectly()
    {
        var correlationId = Guid.NewGuid();
        var workflow = new WorkflowInstance(correlationId, 100, 200, "VIP", 150000m);
        await _dbContext.WorkflowInstances.AddAsync(workflow);
        var repo = new WorkflowRepository(_dbContext);

        // DeductWallet completed
        var deductStep = new WorkflowStep(correlationId, "DeductWallet");
        deductStep.Complete();
        await repo.AddStepAsync(deductStep);

        // UpgradeListing started
        var upgradeStep = new WorkflowStep(correlationId, "UpgradeListing");
        await repo.AddStepAsync(upgradeStep);
        await _dbContext.SaveChangesAsync();

        // UpgradeListing failed -> Compensating, RefundWallet started
        const string reason = "Listing has been deleted";
        workflow.MoveTo(WorkflowStatus.Compensating, reason);
        upgradeStep.Fail(reason);
        var refundStep = new WorkflowStep(correlationId, "RefundWallet");
        await repo.AddStepAsync(refundStep);
        await _dbContext.SaveChangesAsync();

        var stepsCompensating = await repo.GetStepsByCorrelationIdAsync(correlationId);
        Assert.Equal(3, stepsCompensating.Count);
        Assert.Equal("DeductWallet", stepsCompensating[0].StepName);
        Assert.Equal("Completed", stepsCompensating[0].Status);
        Assert.Equal("UpgradeListing", stepsCompensating[1].StepName);
        Assert.Equal("Failed", stepsCompensating[1].Status);
        Assert.Equal(reason, stepsCompensating[1].Error);
        Assert.Equal("RefundWallet", stepsCompensating[2].StepName);
        Assert.Equal("Started", stepsCompensating[2].Status);
        Assert.Null(stepsCompensating[2].FinishedAt);

        // RefundWallet completed -> Compensated
        workflow.MoveTo(WorkflowStatus.Compensated);
        refundStep.Complete();
        await _dbContext.SaveChangesAsync();

        var finalSteps = await repo.GetStepsByCorrelationIdAsync(correlationId);
        Assert.Equal(3, finalSteps.Count);
        Assert.Equal("RefundWallet", finalSteps[2].StepName);
        Assert.Equal("Completed", finalSteps[2].Status);
        Assert.NotNull(finalSteps[2].FinishedAt);
        Assert.Equal(WorkflowStatus.Compensated, workflow.Status);
    }

    [Fact]
    public async Task WorkflowServiceGetByCorrelationIdAsyncMapsWorkflowAndStepsChronologically()
    {
        var correlationId = Guid.NewGuid();
        var workflow = new WorkflowInstance(correlationId, listingId: 10, userId: 100, packageCode: "VIP", chargeAmount: 150000m);
        await _dbContext.WorkflowInstances.AddAsync(workflow);

        var repo = new WorkflowRepository(_dbContext);
        var deductStep = new WorkflowStep(correlationId, "DeductWallet");
        deductStep.Complete();
        await repo.AddStepAsync(deductStep);

        var upgradeStep = new WorkflowStep(correlationId, "UpgradeListing");
        await repo.AddStepAsync(upgradeStep);

        await _dbContext.SaveChangesAsync();

        var service = new WorkflowService(repo, _dbContext);
        var result = await service.GetByCorrelationIdAsync(correlationId);

        Assert.NotNull(result);
        Assert.Equal(correlationId, result.CorrelationId);
        Assert.Equal(10, result.ListingId);
        Assert.Equal(100, result.UserId);
        Assert.Equal("VIP", result.PackageCode);
        Assert.Equal(150000m, result.ChargeAmount);
        Assert.Equal(WorkflowStatus.Started, result.Status);
        Assert.True(result.CreatedAt <= DateTimeOffset.UtcNow);
        Assert.True(result.UpdatedAt <= DateTimeOffset.UtcNow);

        Assert.Equal(2, result.Steps.Count);
        Assert.Equal("DeductWallet", result.Steps[0].StepName);
        Assert.Equal("Completed", result.Steps[0].Status);
        Assert.NotNull(result.Steps[0].FinishedAt);

        Assert.Equal("UpgradeListing", result.Steps[1].StepName);
        Assert.Equal("Started", result.Steps[1].Status);
        Assert.Null(result.Steps[1].FinishedAt);
        Assert.True(result.Steps[0].StartedAt <= result.Steps[1].StartedAt);
    }

    [Fact]
    public async Task WorkflowServiceGetByCorrelationIdAsyncReturnsNullWhenNotFound()
    {
        var repo = new WorkflowRepository(_dbContext);
        var service = new WorkflowService(repo, _dbContext);

        var result = await service.GetByCorrelationIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}
