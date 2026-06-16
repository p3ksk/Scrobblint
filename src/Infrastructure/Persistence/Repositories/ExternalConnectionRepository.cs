using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;

namespace Scrobblint.Infrastructure.Persistence.Repositories;

public sealed class ExternalConnectionRepository : IExternalConnectionRepository
{
    private readonly ScrobblintDbContext _context;
    private readonly IDbContextFactory<ScrobblintDbContext> _factory;

    // Reads use a short-lived context from the factory; writes share the scoped _context with the unit of work.
    public ExternalConnectionRepository(ScrobblintDbContext context, IDbContextFactory<ScrobblintDbContext> factory)
    {
        _context = context;
        _factory = factory;
    }

    public async Task<IReadOnlyList<ExternalConnection>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.ExternalConnections.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Provider)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExternalConnection>> GetEnabledByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.ExternalConnections.AsNoTracking()
            .Where(c => c.UserId == userId && c.IsEnabled)
            .ToListAsync(cancellationToken);
    }

    public async Task<ExternalConnection?> GetAsync(Guid userId, ScrobbleProvider provider, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.ExternalConnections.FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == provider, cancellationToken);
    }

    public async Task AddAsync(ExternalConnection connection, CancellationToken cancellationToken = default) =>
        await _context.ExternalConnections.AddAsync(connection, cancellationToken);

    public void Update(ExternalConnection connection) => _context.ExternalConnections.Update(connection);

    public void Remove(ExternalConnection connection) => _context.ExternalConnections.Remove(connection);
}
