using Scrobblint.Application.Common;
using Scrobblint.Domain.Enums;
using Scrobblint.Shared.Connections;
using Scrobblint.Shared.Relay;

namespace Scrobblint.Application.Services;

/// <summary>
/// Manages a user's links to external scrobbling services (Last.fm, ListenBrainz).
/// </summary>
public interface IExternalConnectionService
{
    Task<Result<ConnectionsResponse>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result> ConnectListenBrainzAsync(Guid userId, ConnectListenBrainzRequest request, CancellationToken cancellationToken = default);

    /// <summary>Builds the Last.fm authorize URL the user must visit; <paramref name="callbackUrl"/> receives the token.</summary>
    Result<string> BeginLastfmAuth(string callbackUrl);

    /// <summary>Completes Last.fm linking with the token returned to the callback.</summary>
    Task<Result> CompleteLastfmAuthAsync(Guid userId, string token, CancellationToken cancellationToken = default);

    Task<Result> SetEnabledAsync(Guid userId, ScrobbleProvider provider, bool enabled, CancellationToken cancellationToken = default);

    Task<Result> DisconnectAsync(Guid userId, ScrobbleProvider provider, CancellationToken cancellationToken = default);

    /// <summary>Lists the caller's own stuck relay records (pending or permanently failed).</summary>
    Task<Result<IReadOnlyList<UserFailedRelayDto>>> GetFailedRelaysAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Resets one of the caller's own retry-cache records so it's retried on the worker's next poll.</summary>
    Task<Result> RetryFailedRelayAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<Result> DeleteFailedRelayAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
