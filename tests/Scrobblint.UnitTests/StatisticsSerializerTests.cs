using System.Text.Json.Nodes;
using Scrobblint.Application.Common;
using Scrobblint.Shared.Stats;
using Xunit;

namespace Scrobblint.UnitTests;

public class StatisticsSerializerTests
{
    private static StatsResponse Sample() => new(
        TotalScrobbles: 3,
        UniqueArtists: 2,
        UniqueTracks: 2,
        UniqueAlbums: 2,
        TopArtists: new[] { new ArtistCount("Radiohead", 2) },
        TopAlbums: new[] { new AlbumCount("Radiohead", "In Rainbows", 2) },
        TopTracks: new[] { new TrackCount("Radiohead", "Nude", 2) },
        MonthlyChart: new[] { new ChartPoint("2026-01", 3) },
        DailyChart: new[] { new ChartPoint("2026-01-01", 3) },
        HourlyChart: new[] { new ChartPoint("12", 3) },
        DayOfWeekChart: new[] { new ChartPoint("Thu", 3) },
        YearlyChart: new[] { new ChartPoint("2026", 3) },
        DayHourHeatmap: new DayHourHeatmap(new[] { new[] { 0, 0, 0 } }));

    [Fact]
    public void User_stats_round_trip()
    {
        var json = StatisticsSerializer.SerializeUserStats(Sample());

        var back = StatisticsSerializer.DeserializeUserStats(json);

        Assert.NotNull(back);
        Assert.Equal(3, back!.TotalScrobbles);
        Assert.Equal("Radiohead", back.TopArtists[0].Artist);
        Assert.Equal("Nude", back.TopTracks[0].Track);
        Assert.Equal("2026-01", back.MonthlyChart[0].Period);
        Assert.Equal(new[] { 0, 0, 0 }, back.DayHourHeatmap!.Rows[0]);
    }

    [Fact]
    public void Payload_missing_a_required_field_is_rejected()
    {
        var node = JsonNode.Parse(StatisticsSerializer.SerializeUserStats(Sample()))!.AsObject();
        node.Remove("monthlyChart"); // e.g. a snapshot written before that field existed

        Assert.Null(StatisticsSerializer.DeserializeUserStats(node.ToJsonString()));
    }

    [Fact]
    public void Payload_with_a_missing_nested_field_is_rejected()
    {
        var node = JsonNode.Parse(StatisticsSerializer.SerializeUserStats(Sample()))!.AsObject();
        var topArtists = (JsonArray)node["topArtists"]!;
        ((JsonObject)topArtists[0]!).Remove("count");

        Assert.Null(StatisticsSerializer.DeserializeUserStats(node.ToJsonString()));
    }

    [Fact]
    public void Payload_without_the_optional_heatmap_is_accepted()
    {
        var node = JsonNode.Parse(StatisticsSerializer.SerializeUserStats(Sample()))!.AsObject();
        node.Remove("dayHourHeatmap");

        var back = StatisticsSerializer.DeserializeUserStats(node.ToJsonString());

        Assert.NotNull(back);
        Assert.Null(back!.DayHourHeatmap);
        Assert.Equal(3, back.TotalScrobbles);
    }

    [Fact]
    public void Global_stats_round_trip_and_reject_missing_fields()
    {
        var sample = new GlobalStatsResponse(
            10, 2, 5, 7,
            new[] { new ArtistCount("Radiohead", 4) },
            new[] { new AlbumCount("Radiohead", "In Rainbows", 4) },
            new[] { new TrackCount("Radiohead", "Nude", 4) });

        var json = StatisticsSerializer.SerializeGlobalStats(sample);
        Assert.NotNull(StatisticsSerializer.DeserializeGlobalStats(json));

        var node = JsonNode.Parse(json)!.AsObject();
        node.Remove("totalUsers");
        Assert.Null(StatisticsSerializer.DeserializeGlobalStats(node.ToJsonString()));
    }

    [Fact]
    public void Garbage_is_rejected()
    {
        Assert.Null(StatisticsSerializer.DeserializeUserStats("not json"));
        Assert.Null(StatisticsSerializer.DeserializeGlobalStats("{"));
    }
}
