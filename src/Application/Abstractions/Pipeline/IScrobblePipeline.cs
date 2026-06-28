namespace Scrobblint.Application.Abstractions.Pipeline;

/// <summary>
/// A scrobble moving through the processing pipeline.
/// Stage 1 (Enrichment) → Stage 2 (Save to DB) → Stage 3 (Relay to external services)
/// </summary>
public sealed record PipelineScrobble(
    Guid UserId,
    string Artist,
    string Track,
    string? Album,
    DateTime Timestamp,
    DateTime CreatedAt);

/// <summary>
/// Entry point for the scrobble processing pipeline.
/// Enqueues raw scrobbles that will be enriched, saved, and relayed in sequence.
/// </summary>
public interface IScrobblePipelineQueue
{
    /// <summary>Enqueues a scrobble for pipeline processing.</summary>
    bool Enqueue(PipelineScrobble scrobble);

    /// <summary>Streams queued scrobbles until cancellation.</summary>
    IAsyncEnumerable<PipelineScrobble> DequeueAllAsync(CancellationToken cancellationToken);

    /// <summary>Approximate number of scrobbles currently queued.</summary>
    int Count { get; }
}

/// <summary>
/// Stage 2: Save enriched scrobbles to database.
/// </summary>
public interface ISaveQueue
{
    /// <summary>Enqueues an enriched scrobble to be saved to the database.</summary>
    bool Enqueue(PipelineScrobble scrobble);

    /// <summary>Streams queued scrobbles until cancellation.</summary>
    IAsyncEnumerable<PipelineScrobble> DequeueAllAsync(CancellationToken cancellationToken);

    /// <summary>Approximate number of scrobbles currently queued.</summary>
    int Count { get; }
}
