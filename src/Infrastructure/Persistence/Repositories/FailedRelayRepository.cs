using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;

namespace Scrobblint.Infrastructure.Persistence.Repositories;

public sealed class FailedRelayRepository : IFailedRelayRepository
{
    private readonly ScrobblintDbContext _context;
    private readonly IDbContextFactory<ScrobblintDbContext> _factory;

    public FailedRelayRepository(ScrobblintDbContext context, IDbContextFactory<ScrobblintDbContext> factory)
    {
        _context = context;
        _factory = factory;
    }

    public async Task AddAsync(FailedRelay failedRelay, CancellationToken cancellationToken = default) =>
        await _context.FailedRelays.AddAsync(failedRelay, cancellationToken);

    public async Task<IReadOnlyList<FailedRelay>> GetPendingAtAsync(DateTime at, int limit, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.FailedRelays.AsNoTracking()
            .Where(r => r.Status == RelayStatus.Pending && r.NextRetryAt <= at)
            .OrderBy(r => r.NextRetryAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByStatusAsync(RelayStatus status, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.FailedRelays.CountAsync(r => r.Status == status, cancellationToken);
    }

    public void Update(FailedRelay failedRelay) => _context.FailedRelays.Update(failedRelay);
}
