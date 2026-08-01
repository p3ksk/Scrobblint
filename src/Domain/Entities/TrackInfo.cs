namespace Scrobblint.Domain.Entities;

/// <summary>
/// A cached Last.fm metadata lookup for a submitted (artist, track) pair. The enrichment stage
/// consults this table before calling the Last.fm API. Only successful lookups are cached;
/// misses are not persisted and will be re-queried on every subsequent scrobble.
/// </summary>
public class TrackInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Normalised (trimmed, lower-cased) artist as submitted by the client — the lookup key.</summary>
    public string ArtistKey { get; set; } = string.Empty;

    /// <summary>Normalised (trimmed, lower-cased) track as submitted by the client — the lookup key.</summary>
    public string TrackKey { get; set; } = string.Empty;

    /// <summary>Canonical artist spelling from Last.fm.</summary>
    public string? CanonicalArtist { get; set; }

    /// <summary>Canonical track spelling from Last.fm.</summary>
    public string? CanonicalTrack { get; set; }

    /// <summary>Album from Last.fm, if any.</summary>
    public string? CanonicalAlbum { get; set; }

    /// <summary>When this entry was fetched and cached (UTC).</summary>
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
