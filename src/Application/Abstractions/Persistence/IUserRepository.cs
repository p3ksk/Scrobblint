using Scrobblint.Domain.Entities;
using Scrobblint.Shared.Users;

namespace Scrobblint.Application.Abstractions.Persistence;

/// <summary>
/// Persistence operations for <see cref="User"/> aggregates.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Looks up a user by username or e-mail (case-insensitive).</summary>
    Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken = default);

    /// <summary>Resolves the account that owns the given API token, or null.</summary>
    Task<User?> GetByApiTokenAsync(string apiToken, CancellationToken cancellationToken = default);

    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Marks a tracked user as modified.</summary>
    void Update(User user);

    /// <summary>
    /// Returns a single page of users together with their scrobble counts, for the admin list.
    /// The projection and paging run in the database.
    /// </summary>
    Task<(IReadOnlyList<AdminUserListItem> Items, int TotalCount)> GetAdminListAsync(
        int page, int pageSize, string? search, CancellationToken cancellationToken = default);

    /// <summary>Total number of registered users.</summary>
    Task<int> CountAllAsync(CancellationToken cancellationToken = default);
}
