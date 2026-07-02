using System.Text.Json;
using Scrobblint.Domain.Entities;
using Scrobblint.Shared.Scrobbles;
using Scrobblint.Shared.Users;

namespace Scrobblint.Application.Common;

/// <summary>
/// Hand-written entity &lt;-&gt; DTO mapping. No AutoMapper by design — explicit is easier to follow.
/// </summary>
public static class Mappers
{
    public static long ToUnix(DateTime utcValue) =>
        new DateTimeOffset(DateTime.SpecifyKind(utcValue, DateTimeKind.Utc)).ToUnixTimeSeconds();

    public static DateTime FromUnix(long unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;

    public static ScrobbleResponse ToResponse(this Scrobble s) => new(
        s.Id, s.Artist, s.Track, s.Album, ToUnix(s.Timestamp), ToUnix(s.CreatedAt));

    public static ScrobbleResponseLite ToLite(this Scrobble s) => new(
        s.Artist, s.Track, s.Album, ToUnix(s.Timestamp));

    public static UserSettingsDto ToDto(this UserSettings settings) =>
        new(settings.ProfileVisibility, settings.Theme);

    /// <summary>Counts entries in a <c>FailedRelay.TracksJson</c> array, tolerating malformed payloads.</summary>
    public static int CountRelayTracks(string tracksJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(tracksJson);
            return doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.GetArrayLength() : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }
}
