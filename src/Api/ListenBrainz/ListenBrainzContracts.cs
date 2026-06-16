using System.Text.Json.Serialization;

namespace Scrobblint.Api.ListenBrainz;

/// <summary>
/// Body of <c>POST /1/submit-listens</c> in the ListenBrainz wire format. Property names map the
/// snake_case JSON keys ListenBrainz clients send. Unknown keys (e.g. <c>additional_info</c> MBIDs)
/// are ignored by the deserializer.
/// </summary>
public sealed class LbSubmitListensRequest
{
    /// <summary>"single", "import" (persisted listens) or "playing_now" (transient now-playing).</summary>
    [JsonPropertyName("listen_type")] public string? ListenType { get; init; }

    [JsonPropertyName("payload")] public List<LbListen>? Payload { get; init; }
}

public sealed class LbListen
{
    /// <summary>Unix time (seconds) the track was listened to. Absent for "playing_now".</summary>
    [JsonPropertyName("listened_at")] public long? ListenedAt { get; init; }

    [JsonPropertyName("track_metadata")] public LbTrackMetadata? TrackMetadata { get; init; }
}

public sealed class LbTrackMetadata
{
    [JsonPropertyName("artist_name")] public string? ArtistName { get; init; }
    [JsonPropertyName("track_name")] public string? TrackName { get; init; }
    [JsonPropertyName("release_name")] public string? ReleaseName { get; init; }
}
