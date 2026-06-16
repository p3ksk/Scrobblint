namespace Scrobblint.Shared.Scrobbles;

/// <summary>
/// A scrobble submission. <paramref name="Timestamp"/> is a Unix time in seconds;
/// when null or omitted the server stamps "now".
/// </summary>
public sealed record ScrobbleRequest(
    string Artist,
    string Track,
    string? Album = null,
    long? Timestamp = null);

/// <summary>Batch submission payload for POST /api/scrobbles.</summary>
public sealed record ScrobbleBatchRequest(IReadOnlyList<ScrobbleRequest> Scrobbles);

/// <summary>Result of a submission: how many listens were accepted.</summary>
public sealed record ScrobbleSubmitResponse(int Accepted);

/// <summary>A "now playing" update. Artist and Track are required; Album is optional.</summary>
public sealed record NowPlayingRequest(string Artist, string Track, string? Album = null);

/// <summary>Response returned when a user has an active "now playing" signal.</summary>
public sealed record NowPlayingResponse(string Artist, string Track, string? Album, DateTime CachedAt);

/// <summary>A stored scrobble as returned to clients. Timestamps are Unix seconds.</summary>
public sealed record ScrobbleResponse(
    Guid Id,
    string Artist,
    string Track,
    string? Album,
    long Timestamp,
    long CreatedAt);
