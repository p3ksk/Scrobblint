using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Domain.Entities;

namespace Scrobblint.Infrastructure.Persistence.Repositories;

public sealed class TrackInfoRepository : ITrackInfoRepository
{
    private readonly ScrobblintDbContext _context;
    private readonly IDbContextFactory<ScrobblintDbContext> _factory;

    public TrackInfoRepository(ScrobblintDbContext context, IDbContextFactory<ScrobblintDbContext> factory)
    {
        _context = context;
        _factory = factory;
    }

    public async Task<TrackInfo?> GetAsync(string artistKey, string trackKey, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.TrackInfos.AsNoTracking()
            .FirstOrDefaultAsync(t => t.ArtistKey == artistKey && t.TrackKey == trackKey, cancellationToken);
    }

    public async Task<TrackInfo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.TrackInfos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<TrackInfo> Items, int Total)> ListAsync(
        int page, int pageSize, string? search, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        var query = db.TrackInfos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(t =>
                t.ArtistKey.Contains(term) ||
                t.TrackKey.Contains(term) ||
                (t.CanonicalArtist != null && t.CanonicalArtist.Contains(term)) ||
                (t.CanonicalTrack != null && t.CanonicalTrack.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.FetchedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task AddAsync(TrackInfo info, CancellationToken cancellationToken = default) =>
        await _context.TrackInfos.AddAsync(info, cancellationToken);

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.TrackInfos.AsNoTracking().CountAsync(cancellationToken);
    }

    public void Update(TrackInfo info) => _context.TrackInfos.Update(info);
    public void Delete(TrackInfo info) => _context.TrackInfos.Remove(info);
}
