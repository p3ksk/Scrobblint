using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;

namespace Scrobblint.Application.Abstractions.Relay;

/// <summary>
/// Forwards listens to one external scrobbling service. One implementation per provider.
/// </summary>
public interface IScrobbleRelay
{
    ScrobbleProvider Provider { get; }

    /// <summary>
    /// Whether this relay is usable on this server (e.g. Last.fm needs an API key/secret configured).
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>Relays a batch of listens using the credentials stored on <paramref name="connection"/>.</summary>
    Task<RelayResult> SendAsync(ExternalConnection connection, IReadOnlyList<RelayTrack> tracks, CancellationToken cancellationToken = default);
}

/// <summary>ListenBrainz relay — validates and stores a user token.</summary>
public interface IListenBrainzRelay : IScrobbleRelay
{
    /// <summary>Validates a ListenBrainz user token against the given (or default) API root.</summary>
    Task<RelayAuthResult> ValidateTokenAsync(string token, string? apiRoot, CancellationToken cancellationToken = default);
}

/// <summary>
/// Last.fm relay using the web-authorization flow: the user authorizes on Last.fm's site and we
/// exchange the returned token for a session key (<c>auth.getSession</c>). No password is handled.
/// </summary>
public interface ILastfmRelay : IScrobbleRelay
{
    /// <summary>
    /// Builds the Last.fm authorize URL to send the user to. After the user approves, Last.fm
    /// redirects to <paramref name="callbackUrl"/> with a <c>token</c> query parameter.
    /// </summary>
    string BuildAuthorizeUrl(string callbackUrl);

    /// <summary>Exchanges an authorized request token for a session key via <c>auth.getSession</c>.</summary>
    Task<RelayAuthResult> CompleteAuthorizationAsync(string token, CancellationToken cancellationToken = default);
}
