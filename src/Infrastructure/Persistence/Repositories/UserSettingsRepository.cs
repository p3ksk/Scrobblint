using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Domain.Entities;

namespace Scrobblint.Infrastructure.Persistence.Repositories;

public sealed class UserSettingsRepository : IUserSettingsRepository
{
    private readonly ScrobblintDbContext _context;
    private readonly IDbContextFactory<ScrobblintDbContext> _factory;

    // Reads use a short-lived context from the factory; writes share the scoped _context with the unit of work.
    public UserSettingsRepository(ScrobblintDbContext context, IDbContextFactory<ScrobblintDbContext> factory)
    {
        _context = context;
        _factory = factory;
    }

    public async Task<UserSettings?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(UserSettings settings, CancellationToken cancellationToken = default) =>
        await _context.UserSettings.AddAsync(settings, cancellationToken);

    public void Update(UserSettings settings) => _context.UserSettings.Update(settings);
}
