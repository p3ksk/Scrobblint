namespace Scrobblint.Application.Abstractions.Statistics;

/// <summary>
/// Lets callers (e.g. an admin "recompute now" action) wake <c>StatisticsPrecomputeWorker</c>
/// immediately instead of waiting out its refresh interval.
/// </summary>
public interface IStatisticsPrecomputeTrigger
{
    /// <summary>Requests an immediate run. Safe to call from any thread; coalesces with a pending request.</summary>
    void RequestRun();

    /// <summary>Waits until either a run is requested or <paramref name="timeout"/> elapses.</summary>
    Task WaitForSignalAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
