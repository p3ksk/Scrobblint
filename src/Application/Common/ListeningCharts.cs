using Scrobblint.Shared.Stats;

namespace Scrobblint.Application.Common;

/// <summary>
/// Buckets listen timestamps into the time-based charts in a given timezone. Done in memory because
/// local-time grouping (DST changes, fractional offsets, day rollover) can't be expressed portably in SQL.
/// </summary>
public static class ListeningCharts
{
    private static readonly string[] DayLabels = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    public sealed record Charts(
        IReadOnlyList<ChartPoint> Monthly,
        IReadOnlyList<ChartPoint> Daily,
        IReadOnlyList<ChartPoint> Hourly,
        IReadOnlyList<ChartPoint> DayOfWeek,
        IReadOnlyList<ChartPoint> Yearly,
        IReadOnlyList<IReadOnlyList<int>> DayHourHeatmap);

    /// <param name="timestampsUtc">Listen times, in UTC.</param>
    /// <param name="zone">Timezone whose local calendar and clock the buckets follow.</param>
    /// <param name="dailyFromUtc">Listens before this instant are left out of the daily chart only.</param>
    public static Charts Build(IEnumerable<DateTime> timestampsUtc, TimeZoneInfo zone, DateTime dailyFromUtc)
    {
        var monthly = new Dictionary<(int Year, int Month), int>();
        var daily = new Dictionary<DateOnly, int>();
        var yearly = new Dictionary<int, int>();
        var hourly = new int[AppConstants.HourlyChartHours];
        var dayHour = new int[7, 24]; // [Monday-based day, hour]

        foreach (var timestamp in timestampsUtc)
        {
            var utc = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);

            monthly[(local.Year, local.Month)] = monthly.GetValueOrDefault((local.Year, local.Month)) + 1;
            yearly[local.Year] = yearly.GetValueOrDefault(local.Year) + 1;
            if (utc >= dailyFromUtc)
            {
                var date = DateOnly.FromDateTime(local);
                daily[date] = daily.GetValueOrDefault(date) + 1;
            }
            hourly[local.Hour]++;
            dayHour[((int)local.DayOfWeek + 6) % 7, local.Hour]++;
        }

        // Heatmap: row 0 = hourly average across the week, rows 1-7 = Monday..Sunday.
        var heatmap = new List<IReadOnlyList<int>>(8);
        var average = new int[24];
        for (var h = 0; h < 24; h++)
        {
            var sum = 0;
            for (var d = 0; d < 7; d++) sum += dayHour[d, h];
            average[h] = (int)Math.Round(sum / 7.0);
        }
        heatmap.Add(average);
        for (var d = 0; d < 7; d++)
        {
            var row = new int[24];
            for (var h = 0; h < 24; h++) row[h] = dayHour[d, h];
            heatmap.Add(row);
        }

        return new Charts(
            Monthly: monthly.OrderBy(kv => kv.Key)
                .Select(kv => new ChartPoint($"{kv.Key.Year:D4}-{kv.Key.Month:D2}", kv.Value)).ToList(),
            Daily: daily.OrderBy(kv => kv.Key)
                .Select(kv => new ChartPoint(kv.Key.ToString("yyyy-MM-dd"), kv.Value)).ToList(),
            Hourly: hourly.Select((count, hour) => new ChartPoint(hour.ToString("D2"), count)).ToList(),
            DayOfWeek: Enumerable.Range(0, 7)
                .Select(d => new ChartPoint(DayLabels[d], Enumerable.Range(0, 24).Sum(h => dayHour[d, h]))).ToList(),
            Yearly: yearly.OrderBy(kv => kv.Key)
                .Select(kv => new ChartPoint(kv.Key.ToString("D4"), kv.Value)).ToList(),
            DayHourHeatmap: heatmap);
    }
}
