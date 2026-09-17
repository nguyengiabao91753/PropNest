namespace PropNest.Application.Abstractions;

public interface IOutboxDispatchService
{
    Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default);
}
