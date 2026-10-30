using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PropNest.Application.Workflows;
using PropNest.Domain.Workflows;
using PropNest.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Infrastructure.Workflows
{
    public partial class PurchasePackageSaga : MassTransitStateMachine<PurchasePackageSagaState>
    {
        // States
        public State Started { get; private set; } = null!;
        public State DeductingWallet { get; private set; } = null!;
        public State WalletDeducted { get; private set; } = null!;
        public State UpgradingListing { get; private set; } = null!;
        public State RecordingHistory { get; private set; } = null!;
        public State Completed { get; private set; } = null!;
        public State Compensating { get; private set; } = null!;
        public State Compensated { get; private set; } = null!;
        public State Failed { get; private set; } = null!;

        // Events
        public Event<PurchasePackageRequested> PurchasePackageRequestedEvent { get; private set; } = null!;
        public Event<WalletDeducted> WalletDeductedEvent { get; private set; } = null!;
        public Event<WalletDeductionFailed> WalletDeductionFailedEvent { get; private set; } = null!;
        public Event<ListingUpgraded> ListingUpgradedEvent { get; private set; } = null!;
        public Event<ListingUpgradeFailed> ListingUpgradeFailedEvent { get; private set; } = null!;
        public Event<WalletRefunded> WalletRefundedEvent { get; private set; } = null!;

        public PurchasePackageSaga()
        {
            InstanceState(x => x.CurrentState);

            // Khai báo CorrelationId mapping cho các Event
            Event(() => PurchasePackageRequestedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));
            Event(() => WalletDeductedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));
            Event(() => WalletDeductionFailedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));
            Event(() => ListingUpgradedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));
            Event(() => ListingUpgradeFailedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));
            Event(() => WalletRefundedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));

            // Step 1: khởi tạo saga -> trừ tiền
            Initially(
                When(PurchasePackageRequestedEvent)
                    .Then(context =>
                    {
                        context.Saga.CorrelationId = context.Message.CorrelationId;
                        context.Saga.ListingId = context.Message.ListingId;
                        context.Saga.UserId = context.Message.UserId;
                        context.Saga.PackageCode = context.Message.PackageCode;
                        context.Saga.ChargeAmount = context.Message.ChargeAmount;
                        context.Saga.CreatedAt = DateTimeOffset.UtcNow;
                        context.Saga.UpdatedAt = DateTimeOffset.UtcNow;
                    })
                    .ThenAsync(async context =>
                    {
                        await StartStepAsync(context, "DeductWallet", WorkflowStatus.DeductingWallet);
                    })
                    .TransitionTo(DeductingWallet)
                    .Publish(context => new DeductWalletCommand(
                        context.Saga.CorrelationId,
                        context.Saga.UserId,
                        context.Saga.ChargeAmount
                    ))
            );


            // Step 2: trong lúc trừ tiền
            During(DeductingWallet,
                When(WalletDeductedEvent)
                    .Then(context =>
                    {
                        context.Saga.UpdatedAt = DateTimeOffset.UtcNow;
                    })
                    .ThenAsync(async context =>
                    {
                        await CompleteAndStartStepAsync(context, completedStepName: "DeductWallet", nextStepName: "UpgradeListing", WorkflowStatus.UpgradingListing);
                    })
                    .TransitionTo(UpgradingListing)
                    .Publish(context => new UpgradeListingCommand(
                        context.Saga.CorrelationId,
                        context.Saga.ListingId,
                        context.Saga.PackageCode
                    )),
                When(WalletDeductionFailedEvent)
                    .Then(context =>
                    {
                        context.Saga.ErrorMessage = context.Message.Reason;
                        context.Saga.UpdatedAt = DateTimeOffset.UtcNow;
                    })
                    .ThenAsync(async context =>
                    {
                        await FailStepAsync(context, "DeductWallet", context.Message.Reason, WorkflowStatus.Failed);
                    })
                    .TransitionTo(Failed)
            );

            // Step 3: nâng cấp tin
            During(UpgradingListing,
                When(ListingUpgradedEvent)
                    .Then(context =>
                    {
                        context.Saga.UpdatedAt = DateTimeOffset.UtcNow;
                    })
                    .ThenAsync(async context =>
                    {
                        await CompleteStepAndRecordHistoryAsync(context, completedStepName: "UpgradeListing", historyStepName: "RecordHistory", WorkflowStatus.Completed);
                    })
                    .TransitionTo(Completed),


                When(ListingUpgradeFailedEvent)
                    .Then(context =>
                    {
                        context.Saga.ErrorMessage = context.Message.Reason;
                        context.Saga.UpdatedAt = DateTimeOffset.UtcNow;
                    })
                    .ThenAsync(async context =>
                    {
                        await FailAndStartCompensationStepAsync(context, failedStepName: "UpgradeListing", error: context.Message.Reason, compensationStepName: "RefundWallet", WorkflowStatus.Compensating);
                    })
                    .TransitionTo(Compensating)
                    .Publish(context => new RefundWalletCommand(
                        context.Saga.CorrelationId,
                        context.Saga.UserId,
                        context.Saga.ChargeAmount))
            );

            // Step 4: hoàn trả tiền
            During(Compensating,
                When(WalletRefundedEvent)
                    .Then(context =>
                    {
                        context.Saga.UpdatedAt = DateTimeOffset.UtcNow;
                    })
                    .ThenAsync(async context =>
                    {
                        await CompleteStepAsync(context, "RefundWallet", WorkflowStatus.Compensated);
                    })
                    .TransitionTo(Compensated)
            );
        }

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "PurchasePackageSaga [{CorrelationId}]: Transitioning to {Status}, Step: {StepName}, Completed: {IsCompleted}, Error: {Error}")]
        private static partial void LogStepTransition(ILogger logger, Guid correlationId, WorkflowStatus status, string stepName, bool isCompleted, string? error);

        private static async Task StartStepAsync<TData>(
            BehaviorContext<PurchasePackageSagaState, TData> context,
            string stepName,
            WorkflowStatus status)
            where TData : class
        {
            var serviceProvider = context.GetPayload<IServiceProvider>();
            var dbContext = serviceProvider.GetRequiredService<PropNestDbContext>();
            var logger = serviceProvider.GetService<ILogger<PurchasePackageSaga>>();
            if (logger is not null)
            {
                LogStepTransition(logger, context.Saga.CorrelationId, status, stepName, isCompleted: false, error: null);
            }

            var workflow = await dbContext.WorkflowInstances.FirstOrDefaultAsync(w => w.CorrelationId == context.Saga.CorrelationId, context.CancellationToken);
            if (workflow is not null)
            {
                workflow.MoveTo(status);
            }

            var existingStarted = await dbContext.WorkflowSteps
                .Where(s => s.CorrelationId == context.Saga.CorrelationId && s.StepName == stepName && s.Status == "Started")
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync(context.CancellationToken);

            if (existingStarted is null)
            {
                var step = new WorkflowStep(context.Saga.CorrelationId, stepName);
                await dbContext.WorkflowSteps.AddAsync(step, context.CancellationToken);
            }

            await dbContext.SaveChangesAsync(context.CancellationToken);
        }

        private static async Task CompleteAndStartStepAsync<TData>(
            BehaviorContext<PurchasePackageSagaState, TData> context,
            string completedStepName,
            string nextStepName,
            WorkflowStatus nextStatus)
            where TData : class
        {
            var serviceProvider = context.GetPayload<IServiceProvider>();
            var dbContext = serviceProvider.GetRequiredService<PropNestDbContext>();
            var logger = serviceProvider.GetService<ILogger<PurchasePackageSaga>>();
            if (logger is not null)
            {
                LogStepTransition(logger, context.Saga.CorrelationId, nextStatus, completedStepName, isCompleted: true, error: null);
                LogStepTransition(logger, context.Saga.CorrelationId, nextStatus, nextStepName, isCompleted: false, error: null);
            }

            var workflow = await dbContext.WorkflowInstances.FirstOrDefaultAsync(w => w.CorrelationId == context.Saga.CorrelationId, context.CancellationToken);
            if (workflow is not null)
            {
                workflow.MoveTo(nextStatus);
            }

            var completedStep = await dbContext.WorkflowSteps
                .Where(s => s.CorrelationId == context.Saga.CorrelationId && s.StepName == completedStepName && s.Status == "Started")
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync(context.CancellationToken);

            if (completedStep is null)
            {
                completedStep = new WorkflowStep(context.Saga.CorrelationId, completedStepName);
                completedStep.Complete();
                await dbContext.WorkflowSteps.AddAsync(completedStep, context.CancellationToken);
            }
            else
            {
                completedStep.Complete();
            }

            var nextStepStarted = await dbContext.WorkflowSteps
                .Where(s => s.CorrelationId == context.Saga.CorrelationId && s.StepName == nextStepName && s.Status == "Started")
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync(context.CancellationToken);

            if (nextStepStarted is null)
            {
                var nextStep = new WorkflowStep(context.Saga.CorrelationId, nextStepName);
                await dbContext.WorkflowSteps.AddAsync(nextStep, context.CancellationToken);
            }

            await dbContext.SaveChangesAsync(context.CancellationToken);
        }

        private static async Task FailStepAsync<TData>(
            BehaviorContext<PurchasePackageSagaState, TData> context,
            string stepName,
            string error,
            WorkflowStatus status)
            where TData : class
        {
            var serviceProvider = context.GetPayload<IServiceProvider>();
            var dbContext = serviceProvider.GetRequiredService<PropNestDbContext>();
            var logger = serviceProvider.GetService<ILogger<PurchasePackageSaga>>();
            if (logger is not null)
            {
                LogStepTransition(logger, context.Saga.CorrelationId, status, stepName, isCompleted: false, error: error);
            }

            var workflow = await dbContext.WorkflowInstances.FirstOrDefaultAsync(w => w.CorrelationId == context.Saga.CorrelationId, context.CancellationToken);
            if (workflow is not null)
            {
                workflow.MoveTo(status, error);
            }

            var step = await dbContext.WorkflowSteps
                .Where(s => s.CorrelationId == context.Saga.CorrelationId && s.StepName == stepName && s.Status == "Started")
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync(context.CancellationToken);

            if (step is null)
            {
                step = new WorkflowStep(context.Saga.CorrelationId, stepName);
                step.Fail(error);
                await dbContext.WorkflowSteps.AddAsync(step, context.CancellationToken);
            }
            else
            {
                step.Fail(error);
            }

            await dbContext.SaveChangesAsync(context.CancellationToken);
        }

        private static async Task FailAndStartCompensationStepAsync<TData>(
            BehaviorContext<PurchasePackageSagaState, TData> context,
            string failedStepName,
            string error,
            string compensationStepName,
            WorkflowStatus status)
            where TData : class
        {
            var serviceProvider = context.GetPayload<IServiceProvider>();
            var dbContext = serviceProvider.GetRequiredService<PropNestDbContext>();
            var logger = serviceProvider.GetService<ILogger<PurchasePackageSaga>>();
            if (logger is not null)
            {
                LogStepTransition(logger, context.Saga.CorrelationId, status, failedStepName, isCompleted: false, error: error);
                LogStepTransition(logger, context.Saga.CorrelationId, status, compensationStepName, isCompleted: false, error: null);
            }

            var workflow = await dbContext.WorkflowInstances.FirstOrDefaultAsync(w => w.CorrelationId == context.Saga.CorrelationId, context.CancellationToken);
            if (workflow is not null)
            {
                workflow.MoveTo(status, error);
            }

            var failedStep = await dbContext.WorkflowSteps
                .Where(s => s.CorrelationId == context.Saga.CorrelationId && s.StepName == failedStepName && s.Status == "Started")
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync(context.CancellationToken);

            if (failedStep is null)
            {
                failedStep = new WorkflowStep(context.Saga.CorrelationId, failedStepName);
                failedStep.Fail(error);
                await dbContext.WorkflowSteps.AddAsync(failedStep, context.CancellationToken);
            }
            else
            {
                failedStep.Fail(error);
            }

            var compensationStarted = await dbContext.WorkflowSteps
                .Where(s => s.CorrelationId == context.Saga.CorrelationId && s.StepName == compensationStepName && s.Status == "Started")
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync(context.CancellationToken);

            if (compensationStarted is null)
            {
                var compStep = new WorkflowStep(context.Saga.CorrelationId, compensationStepName);
                await dbContext.WorkflowSteps.AddAsync(compStep, context.CancellationToken);
            }

            await dbContext.SaveChangesAsync(context.CancellationToken);
        }

        private static async Task CompleteStepAndRecordHistoryAsync<TData>(
            BehaviorContext<PurchasePackageSagaState, TData> context,
            string completedStepName,
            string historyStepName,
            WorkflowStatus status)
            where TData : class
        {
            var serviceProvider = context.GetPayload<IServiceProvider>();
            var dbContext = serviceProvider.GetRequiredService<PropNestDbContext>();
            var logger = serviceProvider.GetService<ILogger<PurchasePackageSaga>>();
            if (logger is not null)
            {
                LogStepTransition(logger, context.Saga.CorrelationId, status, completedStepName, isCompleted: true, error: null);
                LogStepTransition(logger, context.Saga.CorrelationId, status, historyStepName, isCompleted: true, error: null);
            }

            var workflow = await dbContext.WorkflowInstances.FirstOrDefaultAsync(w => w.CorrelationId == context.Saga.CorrelationId, context.CancellationToken);
            if (workflow is not null)
            {
                workflow.MoveTo(status);
            }

            var step = await dbContext.WorkflowSteps
                .Where(s => s.CorrelationId == context.Saga.CorrelationId && s.StepName == completedStepName && s.Status == "Started")
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync(context.CancellationToken);

            if (step is null)
            {
                step = new WorkflowStep(context.Saga.CorrelationId, completedStepName);
                step.Complete();
                await dbContext.WorkflowSteps.AddAsync(step, context.CancellationToken);
            }
            else
            {
                step.Complete();
            }

            var historyStep = await dbContext.WorkflowSteps
                .Where(s => s.CorrelationId == context.Saga.CorrelationId && s.StepName == historyStepName && s.Status == "Started")
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync(context.CancellationToken);

            if (historyStep is null)
            {
                historyStep = new WorkflowStep(context.Saga.CorrelationId, historyStepName);
                historyStep.Complete();
                await dbContext.WorkflowSteps.AddAsync(historyStep, context.CancellationToken);
            }
            else
            {
                historyStep.Complete();
            }

            await dbContext.SaveChangesAsync(context.CancellationToken);
        }

        private static async Task CompleteStepAsync<TData>(
            BehaviorContext<PurchasePackageSagaState, TData> context,
            string stepName,
            WorkflowStatus status)
            where TData : class
        {
            var serviceProvider = context.GetPayload<IServiceProvider>();
            var dbContext = serviceProvider.GetRequiredService<PropNestDbContext>();
            var logger = serviceProvider.GetService<ILogger<PurchasePackageSaga>>();
            if (logger is not null)
            {
                LogStepTransition(logger, context.Saga.CorrelationId, status, stepName, isCompleted: true, error: null);
            }

            var workflow = await dbContext.WorkflowInstances.FirstOrDefaultAsync(w => w.CorrelationId == context.Saga.CorrelationId, context.CancellationToken);
            if (workflow is not null)
            {
                workflow.MoveTo(status);
            }

            var step = await dbContext.WorkflowSteps
                .Where(s => s.CorrelationId == context.Saga.CorrelationId && s.StepName == stepName && s.Status == "Started")
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync(context.CancellationToken);

            if (step is null)
            {
                step = new WorkflowStep(context.Saga.CorrelationId, stepName);
                step.Complete();
                await dbContext.WorkflowSteps.AddAsync(step, context.CancellationToken);
            }
            else
            {
                step.Complete();
            }

            await dbContext.SaveChangesAsync(context.CancellationToken);
        }

        private static async Task RecordStepAsync<TData>(
            BehaviorContext<PurchasePackageSagaState, TData> context,
            WorkflowStatus status,
            string stepName,
            bool isCompleted = false,
            string? error = null)
            where TData : class
        {
            if (isCompleted)
            {
                await CompleteStepAsync(context, stepName, status);
            }
            else if (!string.IsNullOrEmpty(error))
            {
                await FailStepAsync(context, stepName, error, status);
            }
            else
            {
                await StartStepAsync(context, stepName, status);
            }
        }
    }
}
