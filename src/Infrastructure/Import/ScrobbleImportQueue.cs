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

    public bool Enqueue(Guid importId) => _channel.Writer.TryWrite(importId);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
