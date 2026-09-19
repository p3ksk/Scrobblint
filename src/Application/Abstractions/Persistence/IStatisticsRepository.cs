using Scrobblint.Domain.Entities;

namespace Scrobblint.Application.Abstractions.Persistence;

/// <summary>
/// Persistence for the precomputed statistics snapshots served by the read path and refreshed by
/// the background precompute worker. A snapshot is stored as a serialized stats payload.
/// </summary>
public interface IStatisticsRepository
{
    /// <summary>The stored all-time snapshot for a user, or null when none has been computed yet.</summary>
    Task<UserStatistics?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The stored site-wide snapshot, or null when none has been computed yet.</summary>
    Task<GlobalStatistics?> GetGlobalAsync(CancellationToken cancellationToken = default);

    /// <summary>Inserts or replaces a user's snapshot.</summary>
    Task UpsertUserAsync(Guid userId, string payloadJson, DateTime computedAt, CancellationToken cancellationToken = default);

    /// <summary>Inserts or replaces the site-wide snapshot.</summary>
    Task UpsertGlobalAsync(string payloadJson, DateTime computedAt, CancellationToken cancellationToken = default);

    /// <summary>Removes a user's snapshot so the next read recomputes it. Returns true if a row was deleted.</summary>
    Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
