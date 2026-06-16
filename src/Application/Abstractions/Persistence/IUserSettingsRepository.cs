using Scrobblint.Domain.Entities;

namespace Scrobblint.Application.Abstractions.Persistence;

/// <summary>
/// Persistence operations for the 1:1 <see cref="UserSettings"/> record.
/// </summary>
public interface IUserSettingsRepository
{
    Task<UserSettings?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(UserSettings settings, CancellationToken cancellationToken = default);

    void Update(UserSettings settings);
}
