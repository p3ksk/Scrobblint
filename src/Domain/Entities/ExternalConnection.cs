using Scrobblint.Domain.Enums;

namespace Scrobblint.Domain.Entities;

/// <summary>
/// A user's link to an external scrobbling service. When enabled, every listen the user submits
/// to Scrobblint is relayed onward to that service.
/// </summary>
public class ExternalConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public ScrobbleProvider Provider { get; set; }

    /// <summary>Whether listens are currently forwarded to this service.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// The bearer credential: a ListenBrainz user token, or a Last.fm session key.
    /// Stored verbatim because both act as long-lived secrets the server must replay.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>For ListenBrainz: the API root, allowing a self-hosted instance. Null = default.</summary>
    public string? ApiRoot { get; set; }

    /// <summary>The account name on the remote service (for display only).</summary>
    public string? ExternalUsername { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public User? User { get; set; }
}
