namespace Scrobblint.Application.Abstractions.CoverArt;

/// <summary>
/// Resolves artist images and album cover artwork from an external provider.
/// Results are returned as data URIs so the application never leaks user listening
/// habits to a third-party CDN. Implementations cache the image bytes aggressively.
/// </summary>
public interface ICoverArtProvider
{
    /// <summary>Returns a data URI for an artist's profile picture, or null if not found.</summary>
    Task<string?> GetArtistImageUrlAsync(string artist, CancellationToken ct = default);

    /// <summary>Returns a data URI for an album's cover art, or null if not found.</summary>
    Task<string?> GetAlbumCoverUrlAsync(string artist, string album, CancellationToken ct = default);
}
