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

    /// <summary>
    /// Listen timestamps (UTC) in the range, unordered. The time-based charts bucket these in memory in
    /// the user's timezone, since local-time grouping can't be expressed portably in SQL.
    /// </summary>
    Task<IReadOnlyList<DateTime>> GetTimestampsAsync(
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

    /// <summary>
    /// Ids of enabled users with at least one scrobble received (by server time) at or after
    /// <paramref name="sinceUtc"/>. Used by the statistics precompute worker to target users whose
    /// data may have changed.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetActiveUserIdsAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);

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
