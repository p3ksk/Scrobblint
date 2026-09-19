using System.Text.Json;
using Scrobblint.Shared.Stats;

namespace Scrobblint.Application.Common;

/// <summary>
/// JSON round-tripping for the statistics snapshots persisted by the precompute worker. Uses the
/// web defaults (camelCase) so the stored payload stays provider- and language-neutral.
/// </summary>
public static class StatisticsSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        // Every non-optional constructor parameter of the stats records is required, so a payload
        // written by a different version of the contract is rejected instead of silently
        // deserializing a missing list as null. Callers treat a null result as a cache miss and
        // recompute, which is what keeps a schema change from serving a half-empty snapshot.
        // Optional members (e.g. DayHourHeatmap, which has a default) remain optional.
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            RespectRequiredConstructorParameters = true
        };
    }

    public static string SerializeUserStats(StatsResponse stats) => JsonSerializer.Serialize(stats, Options);

    public static StatsResponse? DeserializeUserStats(string payloadJson)
    {
        try
        {
            return JsonSerializer.Deserialize<StatsResponse>(payloadJson, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string SerializeGlobalStats(GlobalStatsResponse stats) => JsonSerializer.Serialize(stats, Options);

    public static GlobalStatsResponse? DeserializeGlobalStats(string payloadJson)
    {
        try
        {
            return JsonSerializer.Deserialize<GlobalStatsResponse>(payloadJson, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
