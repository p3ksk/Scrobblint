using Scrobblint.Shared.Stats;

namespace Scrobblint.Shared.Stats;

/// <summary>
/// Detail data for a single artist: total plays, first/last heard, and all tracks with counts.
/// </summary>
public sealed record ArtistDetail(
    string Artist,
    int TotalPlays,
    DateTime? FirstPlayed,
    DateTime? LastPlayed,
    IReadOnlyList<TrackCount> Tracks);

/// <summary>
/// Detail data for a single album: total plays, first/last heard, and all tracks with counts.
/// </summary>
public sealed record AlbumDetail(
    string Artist,
    string Album,
    int TotalPlays,
    DateTime? FirstPlayed,
    DateTime? LastPlayed,
    IReadOnlyList<TrackCount> Tracks);
