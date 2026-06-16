using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Domain.Entities;

namespace Scrobblint.Infrastructure.Persistence.Repositories;

public sealed class UserSettingsRepository : IUserSettingsRepository
{
    private readonly ScrobblintDbContext _context;

    public UserSettingsRepository(ScrobblintDbContext context) => _context = context;

    public Task<UserSettings?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

    public async Task AddAsync(UserSettings settings, CancellationToken cancellationToken = default) =>
        await _context.UserSettings.AddAsync(settings, cancellationToken);

    public void Update(UserSettings settings) => _context.UserSettings.Update(settings);
}
