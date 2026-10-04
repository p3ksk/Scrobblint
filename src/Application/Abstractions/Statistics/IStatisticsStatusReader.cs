using Scrobblint.Shared.Common;

namespace Scrobblint.Application.Abstractions.Statistics;

/// <summary>
/// A snapshot of the statistics-precomputation state for the admin UI. Timestamps are unix seconds
/// (null when nothing has been computed yet) so they render with the shared <c>Format</c> helpers.
/// </summary>
public sealed record StatisticsSnapshotStatus(
    long? GlobalComputedAt,
    int UserSnapshotCount,
    int TotalUsers,
    long? OldestUserComputedAt,
    long? NewestUserComputedAt,
    int ActiveUserCount,
    int StaleActiveUserCount,
    int IntervalMinutes,
    int ActiveWindowDays,
    bool RunOnStartup);

/// <summary>One user's stored statistics snapshot, for the admin listing.</summary>
public sealed record UserStatisticsSummary(
    Guid UserId,
    string Username,
    long ComputedAt,
    int PayloadChars,
    bool IsStale);

/// <summary>
/// Read-only view of the precomputed statistics snapshots (counts, freshness, per-user listing) plus
/// the worker's configuration. The implementation lives in Infrastructure, where the worker options
/// are available.
/// </summary>
public interface IStatisticsStatusReader
{
    Task<StatisticsSnapshotStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<PagedResponse<UserStatisticsSummary>> GetUserSnapshotsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);
}
