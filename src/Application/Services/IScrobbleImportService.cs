using Scrobblint.Application.Common;
using Scrobblint.Shared.Connections;

namespace Scrobblint.Application.Services;

/// <summary>
/// Starts, reports and cancels bulk imports of a user's listening history from Last.fm.
/// </summary>
public interface IScrobbleImportService
{
    /// <summary>Starts (or returns the already-running) Last.fm history import for the user.</summary>
    Task<Result<ImportStatusDto>> StartLastfmImportAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Latest import status for the user, or null if none has ever been started.</summary>
    Task<ImportStatusDto?> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Requests cancellation of the user's active import.</summary>
    Task<Result> CancelAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a single page of the import identified by <paramref name="importId"/> and persists
    /// progress. Returns true while more pages remain. Driven by the background worker, one page per
    /// call (each in its own scope) so the database context never accumulates over a long import.
    /// </summary>
    Task<bool> ProcessNextChunkAsync(Guid importId, CancellationToken cancellationToken = default);
}
