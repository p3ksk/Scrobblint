namespace Scrobblint.Domain.Entities;

/// <summary>
/// A single play (listen) of a track by a user, captured at a point in time.
/// </summary>
public class Scrobble
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string Artist { get; set; } = string.Empty;

    public string Track { get; set; } = string.Empty;

    /// <summary>Optional album / release name.</summary>
    public string? Album { get; set; }

    /// <summary>When the track was listened to (the client-supplied listen time), stored in UTC.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>When the scrobble was received and stored by the server (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public User? User { get; set; }
}
