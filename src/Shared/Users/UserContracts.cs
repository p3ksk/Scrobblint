using Scrobblint.Domain.Enums;

namespace Scrobblint.Shared.Users;

/// <summary>Public-facing profile for GET /api/user/{username}.</summary>
public sealed record UserProfileResponse(
    string Username,
    long CreatedAt,
    bool IsAdmin,
    int TotalScrobbles,
    ScrobbleResponseLite? LatestScrobble);

/// <summary>A trimmed scrobble used inside profile responses.</summary>
public sealed record ScrobbleResponseLite(string Artist, string Track, string? Album, long Timestamp);

/// <summary>User preferences read/written by the settings page.</summary>
public sealed record UserSettingsDto(ProfileVisibility ProfileVisibility, Theme Theme);

/// <summary>Row in the admin user list.</summary>
public sealed record AdminUserListItem(
    Guid Id,
    string Username,
    string Email,
    long CreatedAt,
    bool IsAdmin,
    bool IsDisabled,
    int ScrobbleCount);

/// <summary>Detailed admin view of a single user.</summary>
public sealed record AdminUserDetail(
    Guid Id,
    string Username,
    string Email,
    long CreatedAt,
    bool IsAdmin,
    bool IsDisabled,
    int ScrobbleCount,
    ProfileVisibility ProfileVisibility,
    Theme Theme);
