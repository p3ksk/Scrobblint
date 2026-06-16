namespace Scrobblint.Application.Common;

/// <summary>
/// Builds a stable composite key (artist + track + listened-at) used to detect duplicate scrobbles
/// during history imports. Comparison is case-insensitive on artist/track to avoid near-duplicates.
/// </summary>
public static class ScrobbleKey
{
    // A control character (U+0001) that will not appear in artist/track names.
    private static readonly string Separator = ((char)1).ToString();

    public static string For(string artist, string track, long unixSeconds) =>
        string.Concat(artist.Trim().ToLowerInvariant(), Separator, track.Trim().ToLowerInvariant(), Separator, unixSeconds.ToString());

    public static string For(string artist, string track, DateTime utc) =>
        For(artist, track, Mappers.ToUnix(utc));
}
