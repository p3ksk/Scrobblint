namespace Scrobblint.Application.Abstractions.Relay;

/// <summary>A unit of relay work: forward these listens for this user to their enabled services.</summary>
public sealed record ScrobbleRelayJob(Guid UserId, IReadOnlyList<RelayTrack> Tracks);

/// <summary>
/// In-process queue decoupling scrobble submission from the (slower, failure-prone) forwarding to
/// external services. Producers enqueue; a background dispatcher consumes.
/// </summary>
public interface IScrobbleRelayQueue
{
    /// <summary>Enqueues a job. Non-blocking; returns false only if the queue is shutting down.</summary>
    bool Enqueue(ScrobbleRelayJob job);

    /// <summary>Streams queued jobs until cancellation.</summary>
    IAsyncEnumerable<ScrobbleRelayJob> DequeueAllAsync(CancellationToken cancellationToken);

    /// <summary>Approximate number of jobs currently queued.</summary>
    int Count { get; }

    /// <summary>Signals the queue that no more items will be enqueued so consumers can drain and exit.</summary>
    void Complete();
}
