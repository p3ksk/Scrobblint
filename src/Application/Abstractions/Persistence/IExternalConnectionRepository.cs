using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;

namespace Scrobblint.Application.Abstractions.Persistence;

/// <summary>
/// Persistence for a user's links to external scrobbling services.
/// </summary>
public interface IExternalConnectionRepository
{
    Task<IReadOnlyList<ExternalConnection>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Returns the user's enabled connections (those that should actually receive relays).</summary>
    Task<IReadOnlyList<ExternalConnection>> GetEnabledByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ExternalConnection?> GetAsync(Guid userId, ScrobbleProvider provider, CancellationToken cancellationToken = default);

    Task AddAsync(ExternalConnection connection, CancellationToken cancellationToken = default);

    void Update(ExternalConnection connection);

    void Remove(ExternalConnection connection);
}
