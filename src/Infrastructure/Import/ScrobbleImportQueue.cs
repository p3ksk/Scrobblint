using System.Threading.Channels;
using Scrobblint.Application.Abstractions.Relay;

namespace Scrobblint.Infrastructure.Import;

/// <summary>
/// Singleton channel that hands import job ids to the background worker. Bounded only by the number
/// of distinct jobs, which is tiny.
/// </summary>
public sealed class ScrobbleImportQueue : IScrobbleImportQueue
{
    private readonly Channel<Guid> _channel =
        Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private int _count;

    public bool Enqueue(Guid importId)
    {
        if (_channel.Writer.TryWrite(importId))
        {
            Interlocked.Increment(ref _count);
            return true;
        }
        return false;
    }

    public async IAsyncEnumerable<Guid> DequeueAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var id in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _count);
            yield return id;
        }
    }

    public int Count => _count;
}
