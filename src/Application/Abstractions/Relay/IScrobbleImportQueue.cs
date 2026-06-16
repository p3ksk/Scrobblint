namespace Scrobblint.Application.Abstractions.Relay;

/// <summary>
/// Signals the background import worker that an import job is ready to be processed (or resumed).
/// </summary>
public interface IScrobbleImportQueue
{
    bool Enqueue(Guid importId);

    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);
}
