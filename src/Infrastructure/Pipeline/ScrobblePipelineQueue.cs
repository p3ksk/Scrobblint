using System.Threading.Channels;
using Scrobblint.Application.Abstractions.Pipeline;

namespace Scrobblint.Infrastructure.Pipeline;

/// <summary>
/// Entry point channel for the scrobble processing pipeline.
/// </summary>
public sealed class ScrobblePipelineQueue : IScrobblePipelineQueue
{
    private readonly Channel<PipelineScrobble> _channel =
        Channel.CreateUnbounded<PipelineScrobble>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private int _count;

    public bool Enqueue(PipelineScrobble scrobble)
    {
        if (_channel.Writer.TryWrite(scrobble))
        {
            Interlocked.Increment(ref _count);
            return true;
        }
        return false;
    }

    public async IAsyncEnumerable<PipelineScrobble> DequeueAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var scrobble in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _count);
            yield return scrobble;
        }
    }

    public int Count => _count;
}
