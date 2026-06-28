using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Domain.Entities;

namespace Scrobblint.Infrastructure.Persistence.Repositories;

public sealed class TrackInfoRepository : ITrackInfoRepository
{
    private readonly ScrobblintDbContext _context;
    private readonly IDbContextFactory<ScrobblintDbContext> _factory;

    // Reads use a short-lived context from the factory; writes share the scoped _context with the
    // unit of work — matching the other repositories.
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

    public async Task AddAsync(TrackInfo info, CancellationToken cancellationToken = default) =>
        await _context.TrackInfos.AddAsync(info, cancellationToken);
}
