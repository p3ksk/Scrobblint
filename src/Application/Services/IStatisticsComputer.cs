using Scrobblint.Shared.Stats;

namespace Scrobblint.Application.Services;

/// <summary>
/// Computes statistics without applying profile visibility, so the background precompute worker can
/// build and persist snapshots by user id. Read paths go through <see cref="IStatisticsService"/>,
/// which enforces visibility.
/// </summary>
public interface IStatisticsComputer
{
    /// <summary>All-time statistics for a user, or null when the user does not exist or is disabled.</summary>
    Task<StatsResponse?> ComputeUserStatsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Site-wide statistics across all users.</summary>
    Task<GlobalStatsResponse> ComputeGlobalStatsAsync(CancellationToken cancellationToken = default);
}
