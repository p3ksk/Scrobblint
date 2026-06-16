using Scrobblint.Application.Common;
using Scrobblint.Shared.Stats;

namespace Scrobblint.Application.Services;

/// <summary>
/// Builds the statistics dashboard for a user, respecting profile visibility.
/// </summary>
public interface IStatisticsService
{
    Task<Result<StatsResponse>> GetStatsAsync(
        string username, ViewerContext viewer, CancellationToken cancellationToken = default);
}
