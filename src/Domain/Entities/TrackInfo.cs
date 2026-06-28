namespace Scrobblint.Domain.Entities;

/// <summary>
/// A cached Last.fm metadata lookup for a submitted (artist, track) pair. The enrichment stage
/// consults this table before calling the Last.fm API, so a track that has been seen once is never
/// looked up again. A row exists for every lookup that has been performed — including ones Last.fm
/// had no match for (<see cref="Found"/> = false), which prevents re-querying unknown tracks.
/// </summary>
public class TrackInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Normalised (trimmed, lower-cased) artist as submitted by the client — the lookup key.</summary>
    public string ArtistKey { get; set; } = string.Empty;

    /// <summary>Normalised (trimmed, lower-cased) track as submitted by the client — the lookup key.</summary>
    public string TrackKey { get; set; } = string.Empty;

    /// <summary>Whether Last.fm returned metadata for this pair. False rows are a negative cache.</summary>
    public bool Found { get; set; }

    /// <summary>Canonical artist spelling from Last.fm. Null when <see cref="Found"/> is false.</summary>
    public string? CanonicalArtist { get; set; }

    /// <summary>Canonical track spelling from Last.fm. Null when <see cref="Found"/> is false.</summary>
    public string? CanonicalTrack { get; set; }

    /// <summary>Album from Last.fm, if any.</summary>
    public string? CanonicalAlbum { get; set; }

    /// <summary>When this entry was fetched and cached (UTC).</summary>
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
