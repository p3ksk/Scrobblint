namespace Scrobblint.Domain.Entities;

/// <summary>
/// A precomputed snapshot of the site-wide statistics, refreshed by the background precompute
/// worker. A single row (id 1) holds the serialized global stats response.
/// </summary>
public class GlobalStatistics
{
    /// <summary>Fixed primary key; the table always holds exactly one row.</summary>
    public int Id { get; set; }

    /// <summary>Serialized global stats response payload.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>When the snapshot was computed (UTC).</summary>
    public DateTime ComputedAt { get; set; }
}
