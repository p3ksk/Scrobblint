using Scrobblint.Domain.Entities;

namespace Scrobblint.Application.Abstractions.Persistence;

/// <summary>
/// Cache of Last.fm metadata lookups, keyed by the normalised (artist, track) the client submitted.
/// Backs the enrichment stage so repeat plays of a track never re-hit the Last.fm API.
/// </summary>
public interface ITrackInfoRepository
{
    /// <summary>Returns the cached lookup for a normalised (artist, track) pair, or null on a miss.</summary>
    Task<TrackInfo?> GetAsync(string artistKey, string trackKey, CancellationToken cancellationToken = default);

    /// <summary>Stages a new cache entry. Persisted via the unit of work.</summary>
    Task AddAsync(TrackInfo info, CancellationToken cancellationToken = default);
}
