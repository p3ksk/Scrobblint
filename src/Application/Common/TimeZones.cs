namespace Scrobblint.Application.Common;

/// <summary>Helpers for working with a user's configured timezone.</summary>
public static class TimeZones
{
    /// <summary>Resolves a stored timezone id, falling back to UTC when it is unset or unknown on this host.</summary>
    public static TimeZoneInfo FindOrUtc(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }

    /// <summary>The current calendar date in <paramref name="zone"/>.</summary>
    public static DateOnly Today(TimeZoneInfo zone, DateTime utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone));

    /// <summary>The UTC instant at which <paramref name="date"/> begins in <paramref name="zone"/>.</summary>
    public static DateTime StartOfDayUtc(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        // Where DST starts at midnight, the day begins at the first valid local time.
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(15);

        // Where DST ends at midnight, the day begins at the earlier of the two instants.
        if (zone.IsAmbiguousTime(local))
            return new DateTimeOffset(local, zone.GetAmbiguousTimeOffsets(local).Max()).UtcDateTime;

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }
}
