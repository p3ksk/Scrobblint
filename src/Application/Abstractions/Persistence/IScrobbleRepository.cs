using Scrobblint.Domain.Entities;
using Scrobblint.Shared.Stats;

namespace Scrobblint.Application.Abstractions.Persistence;

/// <summary>
/// Persistence and aggregation operations for <see cref="Scrobble"/> records.
/// Aggregations are expressed as projections so they translate to SQL and run in the database.
/// </summary>
public interface IScrobbleRepository
{
    Task AddAsync(Scrobble scrobble, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<Scrobble> scrobbles, CancellationToken cancellationToken = default);

    Task<Scrobble?> GetByIdAsync(Guid scrobbleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns dedup keys (artist/track/timestamp) for the user's scrobbles whose timestamp falls in
    /// [<paramref name="fromUtc"/>, <paramref name="toUtc"/>], used to skip already-imported listens.
    /// </summary>
    Task<HashSet<string>> GetExistingKeysAsync(
        Guid userId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Most recent listens for a user, newest first, paged in the database. Supports optional date range filtering and search.</summary>
    Task<(IReadOnlyList<Scrobble> Items, int TotalCount)> GetRecentAsync(
        Guid userId, int page, int pageSize, DateTime? from = null, DateTime? to = null, string? search = null, CancellationToken cancellationToken = default);

    Task<Scrobble?> GetLatestAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> CountAsync(Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    Task<int> CountDistinctArtistsAsync(Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    Task<int> CountDistinctTracksAsync(Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    Task<int> CountDistinctAlbumsAsync(Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArtistCount>> GetTopArtistsAsync(
        Guid userId, int limit, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlbumCount>> GetTopAlbumsAsync(
        Guid userId, int limit, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrackCount>> GetTopTracksAsync(
        Guid userId, int limit, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    /// <summary>Listens grouped by calendar month (UTC), oldest first.</summary>
    Task<IReadOnlyList<ChartPoint>> GetMonthlyChartAsync(
        Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    /// <summary>Listens grouped by day (UTC). Defaults to the trailing <see cref="AppConstants.DailyChartDays"/> window when no range is given.</summary>
    Task<IReadOnlyList<ChartPoint>> GetDailyChartAsync(
        Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    /// <summary>Listens grouped by hour of day (UTC), all 24 hours including zeros.</summary>
    Task<IReadOnlyList<ChartPoint>> GetHourlyChartAsync(
        Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    /// <summary>Listens grouped by day of week (UTC), all seven days including zeros.</summary>
    Task<IReadOnlyList<ChartPoint>> GetDayOfWeekChartAsync(
        Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    /// <summary>Returns an 8×24 heatmap grid: row 0 is the hourly average, rows 1-7 are Monday-Sunday, columns are hours 0-23 UTC.</summary>
    Task<IReadOnlyList<IReadOnlyList<int>>> GetDayHourHeatmapAsync(
        Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    /// <summary>Listens grouped by calendar year (UTC), oldest first.</summary>
    Task<IReadOnlyList<ChartPoint>> GetYearlyChartAsync(
        Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    // ── Artist / album drill-down ──────────────────────────────────────────

    /// <summary>Total scrobble count for a specific artist.</summary>
    Task<int> CountByArtistAsync(Guid userId, string artist, CancellationToken cancellationToken = default);

    /// <summary>Total scrobble count for a specific album (artist-scoped).</summary>
    Task<int> CountByAlbumAsync(Guid userId, string artist, string album, CancellationToken cancellationToken = default);

    /// <summary>Earliest and most recent listen timestamps for an artist.</summary>
    Task<(DateTime? First, DateTime? Last)> GetPlayRangeByArtistAsync(Guid userId, string artist, CancellationToken cancellationToken = default);

    /// <summary>Earliest and most recent listen timestamps for an album.</summary>
    Task<(DateTime? First, DateTime? Last)> GetPlayRangeByAlbumAsync(Guid userId, string artist, string album, CancellationToken cancellationToken = default);

    /// <summary>Distinct tracks for an artist with play counts, ordered by count desc.</summary>
    Task<IReadOnlyList<TrackCount>> GetTracksByArtistAsync(Guid userId, string artist, CancellationToken cancellationToken = default);

    /// <summary>Distinct tracks for an album with play counts, ordered by count desc.</summary>
    Task<IReadOnlyList<TrackCount>> GetTracksByAlbumAsync(Guid userId, string artist, string album, CancellationToken cancellationToken = default);

    /// <summary>Paginated recent scrobbles for a specific artist, newest first.</summary>
    Task<(IReadOnlyList<Scrobble> Items, int TotalCount)> GetRecentByArtistAsync(Guid userId, string artist, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Paginated recent scrobbles for a specific album, newest first.</summary>
    Task<(IReadOnlyList<Scrobble> Items, int TotalCount)> GetRecentByAlbumAsync(Guid userId, string artist, string album, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Deletes a single scrobble by id if it belongs to the specified user. Returns true if deleted.</summary>
    Task<bool> DeleteAsync(Guid scrobbleId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Total scrobble count across all users.</summary>
    Task<int> CountAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Total distinct artists across all users.</summary>
    Task<int> CountDistinctArtistsGlobalAsync(CancellationToken cancellationToken = default);

    /// <summary>Total distinct tracks across all users.</summary>
    Task<int> CountDistinctTracksGlobalAsync(CancellationToken cancellationToken = default);

    /// <summary>Top artists across all users.</summary>
    Task<IReadOnlyList<ArtistCount>> GetTopArtistsGlobalAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Top albums across all users.</summary>
    Task<IReadOnlyList<AlbumCount>> GetTopAlbumsGlobalAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Top tracks across all users.</summary>
    Task<IReadOnlyList<TrackCount>> GetTopTracksGlobalAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Total scrobble count for a specific track (artist-scoped).</summary>
    Task<int> CountByTrackAsync(Guid userId, string artist, string track, CancellationToken cancellationToken = default);

    /// <summary>Earliest and most recent listen timestamps for a track.</summary>
    Task<(DateTime? First, DateTime? Last)> GetPlayRangeByTrackAsync(Guid userId, string artist, string track, CancellationToken cancellationToken = default);

    /// <summary>Paginated recent scrobbles for a specific track, newest first.</summary>
    Task<(IReadOnlyList<Scrobble> Items, int TotalCount)> GetRecentByTrackAsync(Guid userId, string artist, string track, int page, int pageSize, CancellationToken cancellationToken = default);
}
