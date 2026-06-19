using Microsoft.EntityFrameworkCore;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Common;
using Scrobblint.Domain.Entities;
using Scrobblint.Shared.Users;

namespace Scrobblint.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly ScrobblintDbContext _context;
    private readonly IDbContextFactory<ScrobblintDbContext> _factory;

    // Reads use a short-lived context from the factory (each operation gets its own, so concurrent
    // Blazor rendering can never share one and trip "a second operation was started…"); writes share
    // the scoped _context with the unit of work so a transaction spans multiple repositories.
    public UserRepository(ScrobblintDbContext context, IDbContextFactory<ScrobblintDbContext> factory)
    {
        _context = context;
        _factory = factory;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLower();
        await using var db = _factory.CreateDbContext();
        return await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == normalized, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLower();
        await using var db = _factory.CreateDbContext();
        return await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized, cancellationToken);
    }

    public async Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken = default)
    {
        var normalized = usernameOrEmail.Trim().ToLower();
        await using var db = _factory.CreateDbContext();
        return await db.Users.FirstOrDefaultAsync(
            u => u.Username.ToLower() == normalized || u.Email.ToLower() == normalized, cancellationToken);
    }

    public async Task<User?> GetByApiTokenAsync(string apiToken, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Users.FirstOrDefaultAsync(u => u.ApiToken == apiToken, cancellationToken);
    }

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLower();
        await using var db = _factory.CreateDbContext();
        return await db.Users.AnyAsync(u => u.Username.ToLower() == normalized, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLower();
        await using var db = _factory.CreateDbContext();
        return await db.Users.AnyAsync(u => u.Email.ToLower() == normalized, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await _context.Users.AddAsync(user, cancellationToken);

    public void Update(User user) => _context.Users.Update(user);

    public async Task<(IReadOnlyList<AdminUserListItem> Items, int TotalCount)> GetAdminListAsync(
        int page, int pageSize, string? search, CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateDbContext();
        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);

        // Project scrobble counts in the database via a correlated subquery; format the
        // Unix timestamp in memory (ToUnixTimeSeconds does not translate to SQL).
        var rows = await query
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                u.CreatedAt,
                u.IsAdmin,
                u.IsDisabled,
                ScrobbleCount = db.Scrobbles.Count(s => s.UserId == u.Id)
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new AdminUserListItem(
                r.Id, r.Username, r.Email, Mappers.ToUnix(r.CreatedAt),
                r.IsAdmin, r.IsDisabled, r.ScrobbleCount))
            .ToList();

        return (items, total);
    }

    public async Task<int> CountAllAsync(CancellationToken cancellationToken)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Users.AsNoTracking().CountAsync(cancellationToken);
    }
}
