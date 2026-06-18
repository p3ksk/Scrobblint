namespace Scrobblint.Application.Common;

/// <summary>
/// Centralised in-memory cache keys, so the code that populates a cache entry and the code that
/// invalidates it can never drift apart.
/// </summary>
public static class CacheKeys
{
    /// <summary>
    /// Cached aggregate statistics for a user. Keyed by id (not username) so a scrobble writer can
    /// invalidate it from the user id it already holds, without a username lookup.
    /// </summary>
    public static string Stats(Guid userId) => $"stats:{userId}";

    /// <summary>
    /// Cached artwork URL (artist image or album cover). A successful lookup is cached for
    /// 24 h; null misses are not cached so the provider will retry on the next request.
    /// </summary>
    public static string Artwork(string type, string artist, string? album = null)
        => album is null ? $"artwork:{type}:{artist}" : $"artwork:{type}:{artist}:{album}";
}
