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

    public bool Enqueue(ScrobbleRelayJob job) => _channel.Writer.TryWrite(job);

    public IAsyncEnumerable<ScrobbleRelayJob> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
