using Microsoft.Extensions.Logging;
using Scrobblint.Application.Abstractions;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Common;
using Scrobblint.Domain.Enums;
using Scrobblint.Shared.Stats;

namespace Scrobblint.Application.Services;

/// <summary>
/// Serves all-time statistics from the persisted snapshots maintained by the background precompute
/// worker, so a page load is a single row lookup rather than a sweep of the user's history. On a
/// miss it computes once and stores the result (best-effort). Date-ranged views are unique per range
/// and keep computing on demand.
/// </summary>
public sealed class StatisticsSnapshotService : IStatisticsService
{
    private readonly StatisticsService _inner;
    private readonly IUserRepository _users;
    private readonly IUserSettingsRepository _settings;
    private readonly IStatisticsRepository _snapshots;
    private readonly IClock _clock;
    private readonly ILogger<StatisticsSnapshotService> _logger;

    public StatisticsSnapshotService(
        StatisticsService inner,
        IUserRepository users,
        IUserSettingsRepository settings,
        IStatisticsRepository snapshots,
        IClock clock,
        ILogger<StatisticsSnapshotService> logger)
    {
        _inner = inner;
        _users = users;
        _settings = settings;
        _snapshots = snapshots;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<StatsResponse>> GetStatsAsync(
        string username, ViewerContext viewer,
        DateTime? from = null, DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByUsernameAsync(username, cancellationToken);
        if (user is null || user.IsDisabled)
            return await _inner.GetStatsAsync(username, viewer, from, to, cancellationToken);

        // Enforce visibility before serving a snapshot: the inner service would otherwise do it.
        var settings = await _settings.GetByUserIdAsync(user.Id, cancellationToken);
        var visibility = settings?.ProfileVisibility ?? ProfileVisibility.Public;
        if (visibility == ProfileVisibility.Private && !viewer.CanSeePrivate(user.Id))
            return Result<StatsResponse>.Forbidden("This profile is private.");

        // Date-filtered queries are unique per range — compute them on demand, uncached.
        if (from is not null || to is not null)
            return await _inner.GetStatsAsync(username, viewer, from, to, cancellationToken);

        var snapshot = await _snapshots.GetUserAsync(user.Id, cancellationToken);
        if (snapshot is not null)
        {
            var cached = StatisticsSerializer.DeserializeUserStats(snapshot.PayloadJson);
            if (cached is not null)
                return Result<StatsResponse>.Ok(cached);
        }

        var computed = await _inner.ComputeUserStatsAsync(user.Id, cancellationToken);
        if (computed is null)
            return Result<StatsResponse>.NotFound("User not found.");

        await TryPersistUserAsync(user.Id, computed, cancellationToken);
        return Result<StatsResponse>.Ok(computed);
    }

    public async Task<Result<GlobalStatsResponse>> GetGlobalStatsAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _snapshots.GetGlobalAsync(cancellationToken);
        if (snapshot is not null)
        {
            var cached = StatisticsSerializer.DeserializeGlobalStats(snapshot.PayloadJson);
            if (cached is not null)
                return Result<GlobalStatsResponse>.Ok(cached);
        }

        var computed = await _inner.ComputeGlobalStatsAsync(cancellationToken);
        await TryPersistGlobalAsync(computed, cancellationToken);
        return Result<GlobalStatsResponse>.Ok(computed);
    }

    private async Task TryPersistUserAsync(Guid userId, StatsResponse stats, CancellationToken cancellationToken)
    {
        try
        {
            await _snapshots.UpsertUserAsync(
                userId, StatisticsSerializer.SerializeUserStats(stats), _clock.UtcNow, cancellationToken);
        }
        catch (Exception ex)
        {
            // A failed write must never fail the read it was warming.
            _logger.LogDebug(ex, "Could not persist the statistics snapshot for user {UserId}", userId);
        }
    }

    private async Task TryPersistGlobalAsync(GlobalStatsResponse stats, CancellationToken cancellationToken)
    {
        try
        {
            await _snapshots.UpsertGlobalAsync(
                StatisticsSerializer.SerializeGlobalStats(stats), _clock.UtcNow, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not persist the global statistics snapshot");
        }
    }
}
