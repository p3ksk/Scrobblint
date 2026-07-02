using Scrobblint.Application.Abstractions.Relay;

namespace Scrobblint.Infrastructure.Relay;

public sealed class FailedRelayWorkerTrigger : IFailedRelayWorkerTrigger
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void RequestRun()
    {
        // Coalesce bursts of requests into a single wake-up; a full semaphore means one is already pending.
        try { _signal.Release(); }
        catch (SemaphoreFullException) { }
    }

    public Task WaitForSignalAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _signal.WaitAsync(timeout, cancellationToken);
}
