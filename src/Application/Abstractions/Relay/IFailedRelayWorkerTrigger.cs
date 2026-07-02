namespace Scrobblint.Application.Abstractions.Relay;

/// <summary>
/// Lets callers (e.g. an admin action) wake <c>FailedRelayWorker</c> immediately instead of
/// waiting out its poll interval.
/// </summary>
public interface IFailedRelayWorkerTrigger
{
    /// <summary>Requests an immediate run. Safe to call from any thread; coalesces with a pending request.</summary>
    void RequestRun();

    /// <summary>Waits until either a run is requested or <paramref name="timeout"/> elapses.</summary>
    Task WaitForSignalAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
