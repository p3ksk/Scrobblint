namespace Scrobblint.Infrastructure.Configuration;

/// <summary>
/// Bound from "Statistics". Tunes the background worker that precomputes the statistics snapshots
/// served by the read path.
/// </summary>
public sealed class StatisticsOptions
{
    public const string SectionName = "Statistics";

    /// <summary>How often the snapshots are recomputed, in minutes.</summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Trailing window used to decide which users are active (have scrobbles received recently) and
    /// therefore worth recomputing every run. Others are computed on demand when first requested.
    /// </summary>
    public int ActiveWindowDays { get; set; } = 30;

    /// <summary>Whether to run once at start-up (after a short settle delay) to warm the snapshots.</summary>
    public bool RunOnStartup { get; set; } = true;
}
