using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;

namespace Scrobblint.Infrastructure.Persistence.Repositories;

public sealed class ScrobbleImportRepository : IScrobbleImportRepository
{
    private readonly ScrobblintDbContext _context;

    public ScrobbleImportRepository(ScrobblintDbContext context) => _context = context;

    public Task<ScrobbleImport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.ScrobbleImports.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<ScrobbleImport?> GetLatestForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.ScrobbleImports.AsNoTracking()
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ScrobbleImport?> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.ScrobbleImports
            .Where(i => i.UserId == userId &&
                        (i.Status == ImportStatus.Pending || i.Status == ImportStatus.Running))
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ScrobbleImport>> GetResumableAsync(CancellationToken cancellationToken = default) =>
        await _context.ScrobbleImports.AsNoTracking()
            .Where(i => i.Status == ImportStatus.Pending || i.Status == ImportStatus.Running)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ScrobbleImport import, CancellationToken cancellationToken = default) =>
        await _context.ScrobbleImports.AddAsync(import, cancellationToken);

    public void Update(ScrobbleImport import) => _context.ScrobbleImports.Update(import);
}
