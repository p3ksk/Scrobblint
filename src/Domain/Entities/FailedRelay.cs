using Scrobblint.Domain.Enums;

namespace Scrobblint.Domain.Entities;

public class FailedRelay
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public ScrobbleProvider Provider { get; set; }

    public RelayStatus Status { get; set; } = RelayStatus.Pending;

    /// <summary>JSON array of RelayTrack objects.</summary>
    public string TracksJson { get; set; } = "[]";

    public int RetryCount { get; set; }

    public DateTime NextRetryAt { get; set; } = DateTime.UtcNow;

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
