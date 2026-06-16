using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;

namespace Scrobblint.Infrastructure.Persistence.Repositories;

public sealed class ScrobbleImportRepository : IScrobbleImportRepository
{
    private readonly ScrobblintDbContext _context;
    private readonly IDbContextFactory<ScrobblintDbContext> _factory;

    // Reads use a short-lived context from the factory; writes share the scoped _context with the unit
    // of work. Callers that mutate a read entity always re-attach it via Update before SaveChanges.
    public ScrobbleImportRepository(ScrobblintDbContext context, IDbContextFactory<ScrobblintDbContext> factory)
    {
        _context = context;
        _factory = factory;
    }

    public async Task<ScrobbleImport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.ScrobbleImports.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<ScrobbleImport?> GetLatestForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.ScrobbleImports.AsNoTracking()
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ScrobbleImport?> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.ScrobbleImports
            .Where(i => i.UserId == userId &&
                        (i.Status == ImportStatus.Pending || i.Status == ImportStatus.Running))
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScrobbleImport>> GetResumableAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.ScrobbleImports.AsNoTracking()
            .Where(i => i.Status == ImportStatus.Pending || i.Status == ImportStatus.Running)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ScrobbleImport import, CancellationToken cancellationToken = default) =>
        await _context.ScrobbleImports.AddAsync(import, cancellationToken);

    public void Update(ScrobbleImport import) => _context.ScrobbleImports.Update(import);
}
