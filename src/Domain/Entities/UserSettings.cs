using Scrobblint.Domain.Enums;

namespace Scrobblint.Domain.Entities;

/// <summary>
/// Per-user preferences. One row per user (1:1 with <see cref="User"/>).
/// </summary>
public class UserSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public ProfileVisibility ProfileVisibility { get; set; } = ProfileVisibility.Public;

    public Theme Theme { get; set; } = Theme.System;

    public string? TrackIgnoreRegex { get; set; }
    public string? ArtistIgnoreRegex { get; set; }
    public string? AlbumIgnoreRegex { get; set; }

    // Navigation property
    public User? User { get; set; }
}
