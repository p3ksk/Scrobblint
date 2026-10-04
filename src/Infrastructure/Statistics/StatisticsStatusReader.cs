using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scrobblint.Application.Abstractions;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Abstractions.Statistics;
using Scrobblint.Application.Common;
using Scrobblint.Domain.Entities;
using Scrobblint.Infrastructure.Configuration;
using Scrobblint.Infrastructure.Persistence;
using Scrobblint.Shared.Common;

namespace Scrobblint.Infrastructure.Statistics;

/// <summary>
/// Reads the precomputed statistics snapshots for the admin screen. Lives in Infrastructure (not the
/// Application layer) because it reports the worker's <see cref="StatisticsOptions"/> as well.
/// </summary>
public sealed class StatisticsStatusReader : IStatisticsStatusReader
{
    private readonly IDbContextFactory<ScrobblintDbContext> _factory;
    private readonly IScrobbleRepository _scrobbles;
    private readonly StatisticsOptions _options;
    private readonly IClock _clock;

    public StatisticsStatusReader(
        IDbContextFactory<ScrobblintDbContext> factory,
        IScrobbleRepository scrobbles,
        IOptions<StatisticsOptions> options,
        IClock clock)
    {
        _factory = factory;
        _scrobbles = scrobbles;
        _options = options.Value;
        _clock = clock;
    }

    public async Task<StatisticsSnapshotStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var interval = Math.Max(1, _options.IntervalMinutes);
        var window = Math.Max(1, _options.ActiveWindowDays);
        var cutoff = now.AddMinutes(-interval);
        var activeSince = now.AddDays(-window);

        await using var db = _factory.CreateDbContext();

        // Single-row table: filter on the well-known key so the query is deterministic.
        var global = await db.GlobalStatistics.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == GlobalStatistics.SingletonId, cancellationToken);
        var userSnapshotCount = await db.UserStatistics.AsNoTracking().CountAsync(cancellationToken);
        var totalUsers = await db.Users.AsNoTracking().CountAsync(cancellationToken);

        var oldest = await db.UserStatistics.AsNoTracking()
            .OrderBy(s => s.ComputedAt)
            .Select(s => (DateTime?)s.ComputedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var newest = await db.UserStatistics.AsNoTracking()
            .OrderByDescending(s => s.ComputedAt)
            .Select(s => (DateTime?)s.ComputedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Only active users count as behind: the snapshots of long-inactive users are deliberately
        // frozen until they return, so they must not inflate the stale total.
        var staleActive = await db.UserStatistics.AsNoTracking()
            .CountAsync(s => s.ComputedAt < cutoff && db.Scrobbles.Any(x =>
                x.UserId == s.UserId && x.CreatedAt >= activeSince && x.User != null && !x.User.IsDisabled),
                cancellationToken);

        var activeUserIds = await _scrobbles.GetActiveUserIdsAsync(activeSince, cancellationToken);

        return new StatisticsSnapshotStatus(
            GlobalComputedAt: global is null ? null : Mappers.ToUnix(global.ComputedAt),
            UserSnapshotCount: userSnapshotCount,
            TotalUsers: totalUsers,
            OldestUserComputedAt: oldest is null ? null : Mappers.ToUnix(oldest.Value),
            NewestUserComputedAt: newest is null ? null : Mappers.ToUnix(newest.Value),
            ActiveUserCount: activeUserIds.Count,
            StaleActiveUserCount: staleActive,
            IntervalMinutes: interval,
            ActiveWindowDays: window,
            RunOnStartup: _options.RunOnStartup);
    }

    public async Task<PagedResponse<UserStatisticsSummary>> GetUserSnapshotsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = AppConstants.ClampPage(page);
        pageSize = AppConstants.ClampPageSize(pageSize);
        var cutoff = _clock.UtcNow.AddMinutes(-Math.Max(1, _options.IntervalMinutes));

        await using var db = _factory.CreateDbContext();

        var total = await db.UserStatistics.AsNoTracking().CountAsync(cancellationToken);

        // Every snapshot has a matching user (the FK cascades), so an inner join is safe.
        var rows = await (
                from s in db.UserStatistics.AsNoTracking()
                join u in db.Users.AsNoTracking() on s.UserId equals u.Id
                orderby s.ComputedAt, s.UserId
                select new { s.UserId, u.Username, s.ComputedAt, Chars = s.PayloadJson.Length })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new UserStatisticsSummary(
                r.UserId, r.Username, Mappers.ToUnix(r.ComputedAt), r.Chars, r.ComputedAt < cutoff))
            .ToList();

        return new PagedResponse<UserStatisticsSummary>(items, page, pageSize, total);
    }
}
