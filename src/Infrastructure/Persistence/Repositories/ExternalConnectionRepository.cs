using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;

namespace Scrobblint.Infrastructure.Persistence.Repositories;

public sealed class ExternalConnectionRepository : IExternalConnectionRepository
{
    private readonly ScrobblintDbContext _context;

    public ExternalConnectionRepository(ScrobblintDbContext context) => _context = context;

    public async Task<IReadOnlyList<ExternalConnection>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _context.ExternalConnections.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Provider)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExternalConnection>> GetEnabledByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _context.ExternalConnections.AsNoTracking()
            .Where(c => c.UserId == userId && c.IsEnabled)
            .ToListAsync(cancellationToken);

    public Task<ExternalConnection?> GetAsync(Guid userId, ScrobbleProvider provider, CancellationToken cancellationToken = default) =>
        _context.ExternalConnections.FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == provider, cancellationToken);

    public async Task AddAsync(ExternalConnection connection, CancellationToken cancellationToken = default) =>
        await _context.ExternalConnections.AddAsync(connection, cancellationToken);

    public void Update(ExternalConnection connection) => _context.ExternalConnections.Update(connection);

    public void Remove(ExternalConnection connection) => _context.ExternalConnections.Remove(connection);
}
