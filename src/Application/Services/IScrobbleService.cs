using Scrobblint.Application.Common;
using Scrobblint.Shared.Common;
using Scrobblint.Shared.Scrobbles;
using Scrobblint.Shared.Stats;

namespace Scrobblint.Application.Services;

/// <summary>
/// Submission and retrieval of scrobbles.
/// </summary>
public interface IScrobbleService
{
    Task<Result<ScrobbleSubmitResponse>> SubmitAsync(
        Guid userId, ScrobbleRequest request, CancellationToken cancellationToken = default);

    Task<Result<ScrobbleSubmitResponse>> SubmitBatchAsync(
        Guid userId, ScrobbleBatchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recent scrobbles for <paramref name="username"/>, respecting profile visibility for the
    /// supplied <paramref name="viewer"/>.
    /// </summary>
    Task<Result<PagedResponse<ScrobbleResponse>>> GetRecentAsync(
        string username, int page, int pageSize, ViewerContext viewer,
        DateTime? from = null, DateTime? to = null, string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a now-playing update to all enabled external relays for the user.
    /// Fire-and-forget best-effort — never persists locally.
    /// </summary>
    Task<Result> UpdateNowPlayingAsync(Guid userId, NowPlayingRequest request, CancellationToken cancellationToken = default);

    /// <summary>Returns the user's active now-playing signal, or null if none.</summary>
    NowPlayingResponse? GetNowPlaying(Guid userId);

    /// <summary>Returns the user's active now-playing signal by username, or null.</summary>
    Task<NowPlayingResponse?> GetNowPlayingByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>Detail data for an artist: plays, first/last heard, tracks.</summary>
    Task<Result<ArtistDetail>> GetArtistDetailAsync(string username, string artist, ViewerContext viewer, CancellationToken cancellationToken = default);

    /// <summary>Detail data for an album: plays, first/last heard, tracks.</summary>
    Task<Result<AlbumDetail>> GetAlbumDetailAsync(string username, string artist, string album, ViewerContext viewer, CancellationToken cancellationToken = default);

    /// <summary>Recent scrobbles filtered to a specific artist, respecting profile visibility.</summary>
    Task<Result<PagedResponse<ScrobbleResponse>>> GetRecentByArtistAsync(string username, string artist, int page, int pageSize, ViewerContext viewer, CancellationToken cancellationToken = default);

    /// <summary>Recent scrobbles filtered to a specific album, respecting profile visibility.</summary>
    Task<Result<PagedResponse<ScrobbleResponse>>> GetRecentByAlbumAsync(string username, string artist, string album, int page, int pageSize, ViewerContext viewer, CancellationToken cancellationToken = default);

    /// <summary>Deletes a scrobble owned by the specified user.</summary>
    Task<Result> DeleteAsync(Guid userId, Guid scrobbleId, CancellationToken cancellationToken = default);

    /// <summary>Detail data for a track: plays, first/last heard.</summary>
    Task<Result<TrackDetail>> GetTrackDetailAsync(string username, string artist, string track, ViewerContext viewer, CancellationToken cancellationToken = default);

    /// <summary>Recent scrobbles filtered to a specific track, respecting profile visibility.</summary>
    Task<Result<PagedResponse<ScrobbleResponse>>> GetRecentByTrackAsync(string username, string artist, string track, int page, int pageSize, ViewerContext viewer, CancellationToken cancellationToken = default);
}
