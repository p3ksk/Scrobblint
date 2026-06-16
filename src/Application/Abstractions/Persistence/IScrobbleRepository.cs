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

    /// <summary>
    /// Returns dedup keys (artist/track/timestamp) for the user's scrobbles whose timestamp falls in
    /// [<paramref name="fromUtc"/>, <paramref name="toUtc"/>], used to skip already-imported listens.
    /// </summary>
    Task<HashSet<string>> GetExistingKeysAsync(
        Guid userId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Most recent listens for a user, newest first, paged in the database.</summary>
    Task<(IReadOnlyList<Scrobble> Items, int TotalCount)> GetRecentAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Scrobble?> GetLatestAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> CountDistinctArtistsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> CountDistinctTracksAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> CountDistinctAlbumsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArtistCount>> GetTopArtistsAsync(
        Guid userId, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlbumCount>> GetTopAlbumsAsync(
        Guid userId, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrackCount>> GetTopTracksAsync(
        Guid userId, int limit, CancellationToken cancellationToken = default);

    /// <summary>Listens grouped by calendar month (UTC), oldest first.</summary>
    Task<IReadOnlyList<ChartPoint>> GetMonthlyChartAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Listens grouped by day (UTC) over the trailing <paramref name="days"/> window.</summary>
    Task<IReadOnlyList<ChartPoint>> GetDailyChartAsync(
        Guid userId, int days, CancellationToken cancellationToken = default);

    /// <summary>Listens grouped by hour of day (UTC), all 24 hours including zeros.</summary>
    Task<IReadOnlyList<ChartPoint>> GetHourlyChartAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Listens grouped by day of week (UTC), all seven days including zeros.</summary>
    Task<IReadOnlyList<ChartPoint>> GetDayOfWeekChartAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Listens grouped by calendar year (UTC), oldest first.</summary>
    Task<IReadOnlyList<ChartPoint>> GetYearlyChartAsync(
        Guid userId, CancellationToken cancellationToken = default);
}
