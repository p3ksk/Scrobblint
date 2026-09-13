using Scrobblint.Application.Common;

namespace Scrobblint.Web;

/// <summary>Small display helpers shared by the Razor pages.</summary>
public static class Format
{
    public static string DateTimeUtc(long unixSeconds, string? timezone = null)
    {
        var dt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        var zone = TimeZones.FindOrUtc(timezone);
        if (zone == TimeZoneInfo.Utc)
            return dt.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'");

        dt = TimeZoneInfo.ConvertTime(dt, zone);
        var sign = dt.Offset >= TimeSpan.Zero ? "+" : "-";
        return dt.ToString($"yyyy-MM-dd HH:mm 'UTC{sign}{Math.Abs(dt.Offset.Hours):D2}:{Math.Abs(dt.Offset.Minutes):D2}'");
    }

    public static string DateUtc(long unixSeconds, string? timezone = null) =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(unixSeconds), TimeZones.FindOrUtc(timezone))
            .ToString("yyyy-MM-dd");

    /// <summary>Human-friendly "x minutes ago"; older listens fall back to the date in the given timezone.</summary>
    public static string Ago(long unixSeconds, string? timezone = null)
    {
        var then = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        var span = DateTimeOffset.UtcNow - then;
        if (span < TimeSpan.Zero) return "just now";
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays}d ago";
        return DateUtc(unixSeconds, timezone);
    }

    /// <summary>Today's date in the given timezone.</summary>
    public static DateOnly Today(string? timezone) =>
        TimeZones.Today(TimeZones.FindOrUtc(timezone), DateTime.UtcNow);

    /// <summary>
    /// Parses a yyyy-MM-dd query value and returns the UTC instant at which that day (shifted by
    /// <paramref name="addDays"/>) begins in the given timezone, or null when the value is absent or malformed.
    /// </summary>
    public static DateTime? StartOfDayUtc(string? value, string? timezone, int addDays = 0) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", out var date)
            ? TimeZones.StartOfDayUtc(date.AddDays(addDays), TimeZones.FindOrUtc(timezone))
            : null;
}
