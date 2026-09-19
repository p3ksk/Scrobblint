using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Domain.Entities;

namespace Scrobblint.Infrastructure.Persistence.Repositories;

public sealed class StatisticsRepository : IStatisticsRepository
{
    /// <summary>The site-wide snapshot always lives under this fixed key.</summary>
    private const int GlobalSnapshotId = 1;

    private readonly ScrobblintDbContext _context;
    private readonly IDbContextFactory<ScrobblintDbContext> _factory;

    // Reads use a short-lived context from the factory (one per operation); writes share the scoped
    // _context with the unit of work.
    public StatisticsRepository(ScrobblintDbContext context, IDbContextFactory<ScrobblintDbContext> factory)
    {
        _context = context;
        _factory = factory;
    }

    public async Task<UserStatistics?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.UserStatistics.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
    }

    public async Task<GlobalStatistics?> GetGlobalAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.GlobalStatistics.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == GlobalSnapshotId, cancellationToken);
    }

    public async Task UpsertUserAsync(Guid userId, string payloadJson, DateTime computedAt, CancellationToken cancellationToken = default)
    {
        var existing = await _context.UserStatistics.FindAsync(new object[] { userId }, cancellationToken);
        if (existing is null)
        {
            var entity = new UserStatistics { UserId = userId, PayloadJson = payloadJson, ComputedAt = computedAt };
            _context.UserStatistics.Add(entity);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A concurrent first read persisted the same snapshot between our lookup and insert.
                _context.Entry(entity).State = EntityState.Detached;
            }
            return;
        }

        existing.PayloadJson = payloadJson;
        existing.ComputedAt = computedAt;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertGlobalAsync(string payloadJson, DateTime computedAt, CancellationToken cancellationToken = default)
    {
        var existing = await _context.GlobalStatistics.FindAsync(new object[] { GlobalSnapshotId }, cancellationToken);
        if (existing is null)
        {
            _context.GlobalStatistics.Add(new GlobalStatistics
            {
                Id = GlobalSnapshotId,
                PayloadJson = payloadJson,
                ComputedAt = computedAt
            });
        }
        else
        {
            existing.PayloadJson = payloadJson;
            existing.ComputedAt = computedAt;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var deleted = await _context.UserStatistics
            .Where(s => s.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }
}
