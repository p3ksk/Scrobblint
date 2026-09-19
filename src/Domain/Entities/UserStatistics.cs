namespace Scrobblint.Domain.Entities;

/// <summary>
/// A precomputed snapshot of a user's all-time statistics, refreshed by the background
/// precompute worker. The payload is the serialized stats response, so a page read is a single
/// row lookup instead of a full sweep of the user's listening history.
/// </summary>
public class UserStatistics
{
    /// <summary>Owner of the snapshot; also the primary key (one snapshot per user).</summary>
    public Guid UserId { get; set; }

    /// <summary>Serialized stats response payload.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>When the snapshot was computed (UTC).</summary>
    public DateTime ComputedAt { get; set; }
}
