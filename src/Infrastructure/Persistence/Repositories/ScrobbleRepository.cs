using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Common;
using Scrobblint.Domain.Entities;
using Scrobblint.Shared.Stats;

namespace Scrobblint.Infrastructure.Persistence.Repositories;

public sealed class ScrobbleRepository : IScrobbleRepository
{
    private readonly ScrobblintDbContext _context;
    private readonly IDbContextFactory<ScrobblintDbContext> _factory;

    // Reads use a short-lived context from the factory (one per operation, never shared across
    // concurrent renders); writes share the scoped _context with the unit of work.
    public ScrobbleRepository(ScrobblintDbContext context, IDbContextFactory<ScrobblintDbContext> factory)
    {
        _context = context;
        _factory = factory;
    }

    public async Task AddAsync(Scrobble scrobble, CancellationToken cancellationToken = default) =>
        await _context.Scrobbles.AddAsync(scrobble, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<Scrobble> scrobbles, CancellationToken cancellationToken = default) =>
        await _context.Scrobbles.AddRangeAsync(scrobbles, cancellationToken);

    public async Task<HashSet<string>> GetExistingKeysAsync(
        Guid userId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        // Narrow window (one page worth of consecutive listens): uses the (UserId, Timestamp) index.
        await using var db = _factory.CreateDbContext();
        var rows = await db.Scrobbles.AsNoTracking()
            .Where(s => s.UserId == userId && s.Timestamp >= fromUtc && s.Timestamp <= toUtc)
            .Select(s => new { s.Artist, s.Track, s.Timestamp })
            .ToListAsync(cancellationToken);

        var keys = new HashSet<string>(rows.Count);
        foreach (var r in rows)
            keys.Add(ScrobbleKey.For(r.Artist, r.Track, r.Timestamp));
        return keys;
    }

    public async Task<(IReadOnlyList<Scrobble> Items, int TotalCount)> GetRecentAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        var baseQuery = db.Scrobbles.AsNoTracking().Where(s => s.UserId == userId);

        var total = await baseQuery.CountAsync(cancellationToken);
        var items = await baseQuery
            .OrderByDescending(s => s.Timestamp)
            .ThenByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<Scrobble?> GetLatestAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Scrobbles.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.Timestamp)
            .ThenByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CountAsync(Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await ApplyDateFilter(db.Scrobbles.Where(s => s.UserId == userId), from, to).CountAsync(cancellationToken);
    }

    public async Task<int> CountDistinctArtistsAsync(Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await ApplyDateFilter(db.Scrobbles.Where(s => s.UserId == userId), from, to)
            .Select(s => s.Artist).Distinct().CountAsync(cancellationToken);
    }

    public async Task<int> CountDistinctTracksAsync(Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await ApplyDateFilter(db.Scrobbles.Where(s => s.UserId == userId), from, to)
            .Select(s => new { s.Artist, s.Track }).Distinct().CountAsync(cancellationToken);
    }

    public async Task<int> CountDistinctAlbumsAsync(Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await ApplyDateFilter(db.Scrobbles.Where(s => s.UserId == userId && s.Album != null && s.Album != ""), from, to)
            .Select(s => new { s.Artist, s.Album }).Distinct().CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArtistCount>> GetTopArtistsAsync(Guid userId, int limit, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        var rows = await ApplyDateFilter(db.Scrobbles.Where(s => s.UserId == userId), from, to)
            .GroupBy(s => s.Artist)
            .Select(g => new { Artist = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Artist)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ArtistCount(r.Artist, r.Count)).ToList();
    }

    public async Task<IReadOnlyList<AlbumCount>> GetTopAlbumsAsync(Guid userId, int limit, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        var rows = await ApplyDateFilter(db.Scrobbles.Where(s => s.UserId == userId && s.Album != null && s.Album != ""), from, to)
            .GroupBy(s => new { s.Artist, Album = s.Album! })
            .Select(g => new { g.Key.Artist, g.Key.Album, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Album)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AlbumCount(r.Artist, r.Album, r.Count)).ToList();
    }

    public async Task<IReadOnlyList<TrackCount>> GetTopTracksAsync(Guid userId, int limit, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        var rows = await ApplyDateFilter(db.Scrobbles.Where(s => s.UserId == userId), from, to)
            .GroupBy(s => new { s.Artist, s.Track })
            .Select(g => new { g.Key.Artist, g.Key.Track, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Track)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new TrackCount(r.Artist, r.Track, r.Count)).ToList();
    }

    public async Task<IReadOnlyList<ChartPoint>> GetMonthlyChartAsync(Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        // Group by calendar month in the database; format the label in memory.
        await using var db = _factory.CreateDbContext();
        var rows = await ApplyDateFilter(db.Scrobbles.Where(s => s.UserId == userId), from, to)
            .GroupBy(s => new { s.Timestamp.Year, s.Timestamp.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(r => r.Year).ThenBy(r => r.Month)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ChartPoint($"{r.Year:D4}-{r.Month:D2}", r.Count)).ToList();
    }

    public async Task<IReadOnlyList<ChartPoint>> GetDailyChartAsync(Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        // Default to trailing 30-day window when no range is specified.
        var effectiveFrom = from ?? DateTime.UtcNow.Date.AddDays(-(AppConstants.DailyChartDays - 1));

        await using var db = _factory.CreateDbContext();
        var rows = await ApplyDateFilter(db.Scrobbles.Where(s => s.UserId == userId), effectiveFrom, to)
            .GroupBy(s => new { s.Timestamp.Year, s.Timestamp.Month, s.Timestamp.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
            .OrderBy(r => r.Year).ThenBy(r => r.Month).ThenBy(r => r.Day)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ChartPoint($"{r.Year:D4}-{r.Month:D2}-{r.Day:D2}", r.Count)).ToList();
    }

    public async Task<IReadOnlyList<ChartPoint>> GetHourlyChartAsync(Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        var rows = await ApplyDateFilter(db.Scrobbles.Where(s => s.UserId == userId), from, to)
            .GroupBy(s => s.Timestamp.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var counts = rows.ToDictionary(r => r.Hour, r => r.Count);
        var result = new List<ChartPoint>(AppConstants.HourlyChartHours);
        for (var hour = 0; hour < AppConstants.HourlyChartHours; hour++)
            result.Add(new ChartPoint(hour.ToString("D2"), counts.GetValueOrDefault(hour)));
        return result;
    }

    public async Task<IReadOnlyList<ChartPoint>> GetDayOfWeekChartAsync(Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        var rows = await ApplyDateFilter(db.Scrobbles.Where(s => s.UserId == userId), from, to)
            .GroupBy(s => s.Timestamp.DayOfWeek)
            .Select(g => new { DayOfWeek = (int)g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var counts = rows.ToDictionary(r => r.DayOfWeek, r => r.Count);
        var labels = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        var result = new List<ChartPoint>(AppConstants.DayOfWeekChartDays);
        for (var day = 0; day < AppConstants.DayOfWeekChartDays; day++)
        {
            // Monday = 1 ... Saturday = 6, Sunday = 0.
            var dayOfWeek = day < 6 ? day + 1 : 0;
            result.Add(new ChartPoint(labels[day], counts.GetValueOrDefault(dayOfWeek)));
        }
        return result;
    }

    public async Task<IReadOnlyList<ChartPoint>> GetYearlyChartAsync(Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        var rows = await ApplyDateFilter(db.Scrobbles.Where(s => s.UserId == userId), from, to)
            .GroupBy(s => s.Timestamp.Year)
            .Select(g => new { Year = g.Key, Count = g.Count() })
            .OrderBy(r => r.Year)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ChartPoint(r.Year.ToString("D4"), r.Count)).ToList();
    }

    private static IQueryable<Scrobble> ApplyDateFilter(IQueryable<Scrobble> q, DateTime? from, DateTime? to)
    {
        if (from.HasValue) q = q.Where(s => s.Timestamp >= from.Value);
        if (to.HasValue) q = q.Where(s => s.Timestamp < to.Value);
        return q;
    }
}
