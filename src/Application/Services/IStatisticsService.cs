using Scrobblint.Application.Common;
using Scrobblint.Shared.Stats;

namespace Scrobblint.Application.Services;

/// <summary>
/// Builds the statistics dashboard for a user, respecting profile visibility.
/// </summary>
public interface IStatisticsService
{
    Task<Result<StatsResponse>> GetStatsAsync(
        string username, ViewerContext viewer,
        DateTime? from = null, DateTime? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>Site-wide aggregated statistics across all users.</summary>
    Task<Result<GlobalStatsResponse>> GetGlobalStatsAsync(CancellationToken cancellationToken = default);
}
