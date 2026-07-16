using System.Text.RegularExpressions;

namespace Scrobblint.Application.Common;

public static class ScrobbleIgnoreFilter
{
    public static bool ShouldIgnore(
        string artist, string track, string? album,
        string? artistRegex, string? trackRegex, string? albumRegex)
    {
        return Matches(artist, artistRegex)
            || Matches(track, trackRegex)
            || Matches(album, albumRegex);
    }

    private static bool Matches(string? input, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(input))
            return false;
        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase);
        }
        catch (RegexParseException)
        {
            return false;
        }
    }
}
