using Scrobblint.Domain.Enums;

namespace Scrobblint.Shared.Connections;

/// <summary>A configured external connection (never exposes the stored token).</summary>
public sealed record ExternalConnectionDto(
    ScrobbleProvider Provider,
    bool IsEnabled,
    string? ExternalUsername,
    string? ApiRoot);

/// <summary>The connections overview: what the user has linked, and which providers this server supports.</summary>
public sealed record ConnectionsResponse(
    IReadOnlyList<ExternalConnectionDto> Connections,
    bool ListenBrainzAvailable,
    bool LastfmAvailable);

/// <summary>POST connect ListenBrainz. <paramref name="ApiRoot"/> is optional (self-hosted instance).</summary>
public sealed record ConnectListenBrainzRequest(string Token, string? ApiRoot = null);

/// <summary>POST begin Last.fm web authorization. The caller supplies where Last.fm should return the user.</summary>
public sealed record BeginLastfmAuthRequest(string CallbackUrl);

/// <summary>The Last.fm authorize URL to send the user to.</summary>
public sealed record LastfmAuthUrlResponse(string AuthorizeUrl);

/// <summary>POST complete Last.fm authorization with the token Last.fm returned to the callback.</summary>
public sealed record CompleteLastfmAuthRequest(string Token);

/// <summary>Progress of a history import. <paramref name="Status"/> is the <c>ImportStatus</c> name.</summary>
public sealed record ImportStatusDto(
    string Status,
    int ImportedCount,
    int DuplicateCount,
    int TotalAvailable,
    int Page,
    int TotalPages,
    string? Error,
    long UpdatedAt)
{
    public bool IsActive => Status is "Pending" or "Running";

    /// <summary>Percent complete by pages (0–100), or null until the total is known.</summary>
    public int? PercentComplete => TotalPages <= 0
        ? null
        : Math.Clamp((int)Math.Round((Page - 1) * 100.0 / TotalPages), 0, 100);
}
