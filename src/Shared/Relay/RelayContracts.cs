using Scrobblint.Domain.Enums;

namespace Scrobblint.Shared.Relay;

/// <summary>Row in the admin retry cache (failed relay) list.</summary>
public sealed record AdminFailedRelayListItem(
    Guid Id,
    Guid UserId,
    string Username,
    ScrobbleProvider Provider,
    RelayStatus Status,
    int TrackCount,
    int RetryCount,
    long NextRetryAt,
    string? LastError,
    long CreatedAt,
    long UpdatedAt);

/// <summary>Full admin view of a single retry cache record, including the raw relay payload.</summary>
public sealed record AdminFailedRelayDetail(
    Guid Id,
    Guid UserId,
    string Username,
    ScrobbleProvider Provider,
    RelayStatus Status,
    int RetryCount,
    long NextRetryAt,
    string? LastError,
    string TracksJson,
    long CreatedAt,
    long UpdatedAt);

/// <summary>A user's own stuck relay record, shown on the connections page.</summary>
public sealed record UserFailedRelayDto(
    Guid Id,
    ScrobbleProvider Provider,
    RelayStatus Status,
    int TrackCount,
    int RetryCount,
    string? LastError,
    long UpdatedAt);
