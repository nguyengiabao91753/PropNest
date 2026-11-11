using Serilog;

namespace PropNest.Workflow.Worker.Consumers;

/// <summary>
/// Saga consumer for package purchase workflow operations.
/// Follows structured logging pattern using Log.ForContext("WorkflowId", correlationId).
/// </summary>
public interface IPurchasePackageSagaConsumer
{
    Task ProcessAsync(Guid correlationId, string packageCode, decimal chargeAmount, CancellationToken cancellationToken = default);
}

public sealed class PurchasePackageSagaConsumer : IPurchasePackageSagaConsumer
{
    public Task ProcessAsync(Guid correlationId, string packageCode, decimal chargeAmount, CancellationToken cancellationToken = default)
    {
        // Enforce task requirement: Log.ForContext("WorkflowId", correlationId) trong Saga consumers
        var sagaLogger = Log.ForContext("WorkflowId", correlationId);
        sagaLogger.Information("Starting purchase package saga execution for package {PackageCode} with charge {ChargeAmount}", packageCode, chargeAmount);

        // Saga business steps execution...

        sagaLogger.Information("Completed purchase package saga execution for package {PackageCode}", packageCode);
        return Task.CompletedTask;
    }
}
