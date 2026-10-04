using Scrobblint.Infrastructure.Statistics;
using Xunit;

namespace Scrobblint.UnitTests;

public class StatisticsPrecomputeTriggerTests
{
    [Fact]
    public async Task Request_run_wakes_a_waiter_immediately()
    {
        var trigger = new StatisticsPrecomputeTrigger();
        trigger.RequestRun();

        var wait = trigger.WaitForSignalAsync(TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.Same(wait, await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(5))));
    }

    [Fact]
    public async Task Burst_of_requests_coalesces_into_one_wake_up()
    {
        var trigger = new StatisticsPrecomputeTrigger();
        trigger.RequestRun();
        trigger.RequestRun();
        trigger.RequestRun();

        // The first wait consumes the single pending signal.
        await trigger.WaitForSignalAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        // Nothing is pending now, so a second wait must not complete immediately.
        var second = trigger.WaitForSignalAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.NotSame(second, await Task.WhenAny(second, Task.Delay(TimeSpan.FromMilliseconds(150))));

        trigger.RequestRun(); // release the waiter so the test leaves nothing running
        await second;
    }
}
