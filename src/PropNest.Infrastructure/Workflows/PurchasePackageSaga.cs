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
                        await RecordStepAsync(context, WorkflowStatus.DeductingWallet, "DeductWallet", isCompleted: false);
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
                        await RecordStepAsync(context, WorkflowStatus.WalletDeducted, "DeductWallet", isCompleted: true);
                        await RecordStepAsync(context, WorkflowStatus.UpgradingListing, "UpgradeListing", isCompleted: false);
                    })
                    .TransitionTo(UpgradingListing)
                    .Publish(context => new UpgradeListingCommand(
                        context.Saga.CorrelationId,
                        context.Saga.UserId,
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
                        await RecordStepAsync(context, WorkflowStatus.Failed, "DeductWallet", isCompleted: false, error: context.Message.Reason);
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
                        await RecordStepAsync(context, WorkflowStatus.RecordingHistory, "UpgradeListing", isCompleted: true);
                        await RecordStepAsync(context, WorkflowStatus.Completed, "RecordHistory", isCompleted: true);
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
                        await RecordStepAsync(context, WorkflowStatus.Compensating, "UpgradeListing", isCompleted: false, error: context.Message.Reason);
                        await RecordStepAsync(context, WorkflowStatus.Compensating, "RefundWallet", isCompleted: false);
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
                        await RecordStepAsync(context, WorkflowStatus.Compensated, "RefundWallet", isCompleted: true);
                    })
                    .TransitionTo(Compensated)
            );
        }


       [LoggerMessage(
       Level = LogLevel.Information,
       Message = "PurchasePackageSaga [{CorrelationId}]: Transitioning to {Status}, Step: {StepName}, Completed: {IsCompleted}, Error: {Error}")]
        private static partial void LogStepTransition(ILogger logger, Guid correlationId, WorkflowStatus status, string stepName, bool isCompleted, string? error);
        private static async Task RecordStepAsync<TData>(
            BehaviorContext<PurchasePackageSagaState, TData> context,
            WorkflowStatus status,
            string stepName,
            bool isCompleted = false,
            string? error = null)
            where TData : class
        {
            var serviceProvider = context.GetPayload<IServiceProvider>();
            var dbContext = serviceProvider.GetRequiredService<PropNestDbContext>();
            var logger = serviceProvider.GetService<ILogger<PurchasePackageSaga>>();
            if (logger is not null)
            {
                LogStepTransition(logger, context.Saga.CorrelationId, status, stepName, isCompleted, error);
            }
            var workflow = await dbContext.WorkflowInstances.FirstOrDefaultAsync(w => w.CorrelationId == context.Saga.CorrelationId);
            if (workflow is not null)
            {
                workflow.MoveTo(status, error);
            }
            var step = new WorkflowStep(context.Saga.CorrelationId, stepName);
            if (isCompleted)
            {
                step.Complete();
            }
            else if (!string.IsNullOrEmpty(error))
            {
                step.Fail(error);
            }
            await dbContext.WorkflowSteps.AddAsync(step);
            await dbContext.SaveChangesAsync();
        }
    }
}
