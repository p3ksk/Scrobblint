using Microsoft.Extensions.Options;
using Scrobblint.Application.Abstractions.Statistics;
using Scrobblint.Infrastructure.Configuration;

namespace Scrobblint.Infrastructure.Statistics;

public sealed class StatisticsPrecomputeTrigger : IStatisticsPrecomputeTrigger
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
