using Scrobblint.Application.Common;
using Scrobblint.Domain.Enums;
using Scrobblint.Shared.Connections;

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
}
