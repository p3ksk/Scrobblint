using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Scrobblint.Application.Abstractions.Pipeline;

namespace Scrobblint.Infrastructure.Pipeline;

/// <summary>
/// Entry point channel for the scrobble processing pipeline.
/// </summary>
public sealed class ScrobblePipelineQueue : IScrobblePipelineQueue
{
    private readonly ILogger<ScrobblePipelineQueue> _logger;
    private readonly Channel<PipelineScrobble> _channel;

    private int _count;
    
    public ScrobblePipelineQueue(ILogger<ScrobblePipelineQueue> logger)
    {
        _logger = logger;
        _channel = Channel.CreateUnbounded<PipelineScrobble>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }
    
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
            _logger.LogDebug("Scrobble {Artist} - {Album} - {Track} of user {User} processing started.", scrobble.Artist, scrobble.Album, scrobble.Track, scrobble.UserId);
            yield return scrobble;
        }
    }

    public int Count => _count;
}
