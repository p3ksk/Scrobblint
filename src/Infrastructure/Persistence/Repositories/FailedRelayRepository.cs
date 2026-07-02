using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Common;
using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;
using Scrobblint.Shared.Relay;

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

    public async Task<FailedRelay?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.FailedRelays.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<FailedRelay>> GetByUserIdAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.FailedRelays.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.UpdatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AdminFailedRelayListItem> Items, int TotalCount)> GetAdminListAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        var query = db.FailedRelays.AsNoTracking();

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(r => r.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.UserId,
                Username = r.User!.Username,
                r.Provider,
                r.Status,
                r.TracksJson,
                r.RetryCount,
                r.NextRetryAt,
                r.LastError,
                r.CreatedAt,
                r.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new AdminFailedRelayListItem(
                r.Id, r.UserId, r.Username, r.Provider, r.Status, Mappers.CountRelayTracks(r.TracksJson),
                r.RetryCount, Mappers.ToUnix(r.NextRetryAt), r.LastError,
                Mappers.ToUnix(r.CreatedAt), Mappers.ToUnix(r.UpdatedAt)))
            .ToList();

        return (items, total);
    }

    public async Task<int> ResetAllFailedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        var now = DateTime.UtcNow;
        return await db.FailedRelays
            .Where(r => r.Status == RelayStatus.Failed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, RelayStatus.Pending)
                .SetProperty(r => r.RetryCount, 0)
                .SetProperty(r => r.NextRetryAt, now)
                .SetProperty(r => r.UpdatedAt, now),
                cancellationToken);
    }

    public void Update(FailedRelay failedRelay) => _context.FailedRelays.Update(failedRelay);

    public void Remove(FailedRelay failedRelay) => _context.FailedRelays.Remove(failedRelay);
}
