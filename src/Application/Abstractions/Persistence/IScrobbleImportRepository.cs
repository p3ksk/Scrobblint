using Scrobblint.Domain.Entities;

namespace Scrobblint.Application.Abstractions.Persistence;

/// <summary>Persistence for history-import jobs.</summary>
public interface IScrobbleImportRepository
{
    Task<ScrobbleImport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The user's most recent import (any status), for status display.</summary>
    Task<ScrobbleImport?> GetLatestForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The user's currently active (pending or running) import, if any.</summary>
    Task<ScrobbleImport?> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>All imports left pending/running (e.g. after a restart), so they can be resumed.</summary>
    Task<IReadOnlyList<ScrobbleImport>> GetResumableAsync(CancellationToken cancellationToken = default);

    Task AddAsync(ScrobbleImport import, CancellationToken cancellationToken = default);

    void Update(ScrobbleImport import);
}
