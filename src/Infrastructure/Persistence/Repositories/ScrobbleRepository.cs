using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Domain.Entities;
using Scrobblint.Shared.Stats;

namespace Scrobblint.Infrastructure.Persistence.Repositories;

public sealed class ScrobbleRepository : IScrobbleRepository
{
    private readonly ScrobblintDbContext _context;

    public ScrobbleRepository(ScrobblintDbContext context) => _context = context;

    public async Task AddAsync(Scrobble scrobble, CancellationToken cancellationToken = default) =>
        await _context.Scrobbles.AddAsync(scrobble, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<Scrobble> scrobbles, CancellationToken cancellationToken = default) =>
        await _context.Scrobbles.AddRangeAsync(scrobbles, cancellationToken);

    public async Task<(IReadOnlyList<Scrobble> Items, int TotalCount)> GetRecentAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.Scrobbles.AsNoTracking().Where(s => s.UserId == userId);

        var total = await baseQuery.CountAsync(cancellationToken);
        var items = await baseQuery
            .OrderByDescending(s => s.Timestamp)
            .ThenByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<Scrobble?> GetLatestAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.Scrobbles.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.Timestamp)
            .ThenByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.Scrobbles.Where(s => s.UserId == userId).CountAsync(cancellationToken);

    public Task<int> CountDistinctArtistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.Scrobbles.Where(s => s.UserId == userId)
            .Select(s => s.Artist).Distinct().CountAsync(cancellationToken);

    public Task<int> CountDistinctTracksAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.Scrobbles.Where(s => s.UserId == userId)
            .Select(s => new { s.Artist, s.Track }).Distinct().CountAsync(cancellationToken);

    public async Task<IReadOnlyList<ArtistCount>> GetTopArtistsAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
    {
        var rows = await _context.Scrobbles.Where(s => s.UserId == userId)
            .GroupBy(s => s.Artist)
            .Select(g => new { Artist = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Artist)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ArtistCount(r.Artist, r.Count)).ToList();
    }

    public async Task<IReadOnlyList<AlbumCount>> GetTopAlbumsAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
    {
        var rows = await _context.Scrobbles.Where(s => s.UserId == userId && s.Album != null && s.Album != "")
            .GroupBy(s => new { s.Artist, Album = s.Album! })
            .Select(g => new { g.Key.Artist, g.Key.Album, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Album)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AlbumCount(r.Artist, r.Album, r.Count)).ToList();
    }

    public async Task<IReadOnlyList<TrackCount>> GetTopTracksAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
    {
        var rows = await _context.Scrobbles.Where(s => s.UserId == userId)
            .GroupBy(s => new { s.Artist, s.Track })
            .Select(g => new { g.Key.Artist, g.Key.Track, Count = g.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Track)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new TrackCount(r.Artist, r.Track, r.Count)).ToList();
    }

    public async Task<IReadOnlyList<ChartPoint>> GetMonthlyChartAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Group by calendar month in the database; format the label in memory.
        var rows = await _context.Scrobbles.Where(s => s.UserId == userId)
            .GroupBy(s => new { s.Timestamp.Year, s.Timestamp.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(r => r.Year).ThenBy(r => r.Month)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ChartPoint($"{r.Year:D4}-{r.Month:D2}", r.Count)).ToList();
    }

    public async Task<IReadOnlyList<ChartPoint>> GetDailyChartAsync(Guid userId, int days, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var rows = await _context.Scrobbles
            .Where(s => s.UserId == userId && s.Timestamp >= cutoff)
            .GroupBy(s => new { s.Timestamp.Year, s.Timestamp.Month, s.Timestamp.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
            .OrderBy(r => r.Year).ThenBy(r => r.Month).ThenBy(r => r.Day)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ChartPoint($"{r.Year:D4}-{r.Month:D2}-{r.Day:D2}", r.Count)).ToList();
    }
}
