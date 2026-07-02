namespace Scrobblint.Shared.Stats;

/// <summary>Aggregated artist play count.</summary>
public sealed record ArtistCount(string Artist, int Count);

/// <summary>Aggregated album play count (album scoped to its artist).</summary>
public sealed record AlbumCount(string Artist, string Album, int Count);

/// <summary>Aggregated track play count (track scoped to its artist).</summary>
public sealed record TrackCount(string Artist, string Track, int Count);

/// <summary>A point on a time-series chart, e.g. ("2026-06", 142) or ("2026-06-15", 7).</summary>
public sealed record ChartPoint(string Period, int Count);

/// <summary>
/// The statistics dashboard payload for a user. Top-N lists are bounded server-side.
/// </summary>
public sealed record StatsResponse(
    int TotalScrobbles,
    int UniqueArtists,
    int UniqueTracks,
    int UniqueAlbums,
    IReadOnlyList<ArtistCount> TopArtists,
    IReadOnlyList<AlbumCount> TopAlbums,
    IReadOnlyList<TrackCount> TopTracks,
    IReadOnlyList<ChartPoint> MonthlyChart,
    IReadOnlyList<ChartPoint> DailyChart,
    IReadOnlyList<ChartPoint> HourlyChart,
    IReadOnlyList<ChartPoint> DayOfWeekChart,
    IReadOnlyList<ChartPoint> YearlyChart);

/// <summary>
/// Site-wide aggregated statistics across all users. Restricted to totals + top-N lists
/// (no time-series charts, since they have little meaning across different listening eras).
/// </summary>
public sealed record GlobalStatsResponse(
    int TotalScrobbles,
    int TotalUsers,
    int UniqueArtists,
    int UniqueTracks,
    IReadOnlyList<ArtistCount> TopArtists,
    IReadOnlyList<AlbumCount> TopAlbums,
    IReadOnlyList<TrackCount> TopTracks);
