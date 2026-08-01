namespace Scrobblint.Web;

/// <summary>Small display helpers shared by the Razor pages.</summary>
public static class Format
{
    public static string DateTimeUtc(long unixSeconds, string? timezone = null)
    {
        var dt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        if (timezone is not null)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                dt = TimeZoneInfo.ConvertTime(dt, tz);
                var sign = dt.Offset >= TimeSpan.Zero ? "+" : "-";
                var offset = dt.Offset;
                return dt.ToString($"yyyy-MM-dd HH:mm 'UTC{sign}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}'");
            }
            catch (TimeZoneNotFoundException) { }
        }
        return dt.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'");
    }

    public static string DateUtc(long unixSeconds, string? timezone = null)
    {
        var dt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        if (timezone is not null)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                dt = TimeZoneInfo.ConvertTime(dt, tz);
            }
            catch (TimeZoneNotFoundException) { }
        }
        return dt.ToString("yyyy-MM-dd");
    }

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

    /// <summary>
    /// Rotates an 8×24 heatmap grid (row 0 = average, rows 1-7 = Mon–Sun)
    /// so that columns align to the given IANA timezone instead of UTC.
    /// Returns a new grid with the same shape.
    /// </summary>
    public static int[][] ShiftHeatmap(IReadOnlyList<IReadOnlyList<int>> rows, string timezone)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var offsetHours = (int)tz.GetUtcOffset(DateTime.UtcNow).TotalHours;
        // utcHour = (localHour - offset) mod 24, so to get the UTC column for each local column:
        var shift = ((-offsetHours) % 24 + 24) % 24;

        var result = new int[rows.Count][];
        for (var d = 0; d < rows.Count; d++)
        {
            result[d] = new int[24];
            for (var h = 0; h < 24; h++)
                result[d][h] = rows[d][(h + shift) % 24];
        }
        return result;
    }
}
