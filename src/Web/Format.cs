namespace Scrobblint.Web;

/// <summary>Small display helpers shared by the Razor pages.</summary>
public static class Format
{
    public static string DateTimeUtc(long unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'");

    public static string DateUtc(long unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime.ToString("yyyy-MM-dd");

    /// <summary>Human-friendly "x minutes ago" relative to now (UTC).</summary>
    public static string Ago(long unixSeconds)
    {
        var then = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        var span = DateTimeOffset.UtcNow - then;
        if (span < TimeSpan.Zero) return "just now";
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays}d ago";
        return then.UtcDateTime.ToString("yyyy-MM-dd");
    }
}
