using System.Threading.Channels;
using Scrobblint.Application.Abstractions.Relay;

namespace Scrobblint.Infrastructure.Relay;

/// <summary>
/// Unbounded in-process queue backed by <see cref="Channel"/>. Registered as a singleton so producers
/// (scrobble submissions) and the background dispatcher share one channel.
/// </summary>
public sealed class ScrobbleRelayQueue : IScrobbleRelayQueue
{
    private readonly Channel<ScrobbleRelayJob> _channel =
        Channel.CreateUnbounded<ScrobbleRelayJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private int _count;

    public bool Enqueue(ScrobbleRelayJob job)
    {
        if (_channel.Writer.TryWrite(job))
        {
            Interlocked.Increment(ref _count);
            return true;
        }
        return false;
    }

    public async IAsyncEnumerable<ScrobbleRelayJob> DequeueAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _count);
            yield return job;
        }
    }

    public int Count => _count;

    public void Complete() => _channel.Writer.Complete();
}
