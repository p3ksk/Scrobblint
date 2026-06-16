using Scrobblint.Api.Authentication;
using Scrobblint.Application.Common;
using Scrobblint.Application.Services;
using Scrobblint.Domain.Entities;
using Scrobblint.Shared.Scrobbles;

namespace Scrobblint.Api.ListenBrainz;

/// <summary>
/// A ListenBrainz-compatible API surface mounted at <c>/1</c>, so a ListenBrainz client can be
/// pointed at this server by changing only its API base URL. Implements the subset that scrobble
/// clients use: token validation, listen submission (single / import / playing_now) and read-back.
/// See https://listenbrainz.readthedocs.io/en/latest/users/api/core.html.
/// </summary>
public static class ListenBrainzEndpoints
{
    public static IEndpointRouteBuilder MapListenBrainzApi(this IEndpointRouteBuilder app)
    {
        var lb = app.MapGroup("/1").WithTags("ListenBrainz");

        lb.MapPost("/submit-listens", SubmitListensAsync)
            .WithName("LbSubmitListens")
            .WithSummary("Submit listens in the ListenBrainz format (single, import or playing_now).");

        lb.MapGet("/validate-token", ValidateTokenAsync)
            .WithName("LbValidateToken")
            .WithSummary("Validate a ListenBrainz user token (Authorization header or ?token=).");

        lb.MapGet("/user/{userName}/listens", GetListensAsync)
            .WithName("LbGetListens")
            .WithSummary("Return a user's most recent listens.");

        lb.MapGet("/user/{userName}/playing-now", GetPlayingNowAsync)
            .WithName("LbGetPlayingNow")
            .WithSummary("Return a user's currently-playing track, if any.");

        return app;
    }

    // POST /1/submit-listens
    private static async Task<IResult> SubmitListensAsync(
        HttpContext ctx, LbSubmitListensRequest? body, IAuthService auth, IScrobbleService scrobbles)
    {
        var user = await ResolveUserAsync(ctx, auth);
        if (user is null)
            return Error(StatusCodes.Status401Unauthorized, "Invalid authorization token.");

        if (body?.Payload is null || body.Payload.Count == 0)
            return Error(StatusCodes.Status400BadRequest, "JSON document must contain a payload with at least one listen.");

        var listenType = (body.ListenType ?? "single").Trim().ToLowerInvariant();

        if (listenType == "playing_now")
        {
            var meta = body.Payload[0].TrackMetadata;
            if (string.IsNullOrWhiteSpace(meta?.ArtistName) || string.IsNullOrWhiteSpace(meta.TrackName))
                return Error(StatusCodes.Status400BadRequest, "track_metadata must include artist_name and track_name.");

            var nowPlaying = new NowPlayingRequest(meta.ArtistName!.Trim(), meta.TrackName!.Trim(), NullIfBlank(meta.ReleaseName));
            var npResult = await scrobbles.UpdateNowPlayingAsync(user.Id, nowPlaying, ctx.RequestAborted);
            return npResult.Succeeded ? Ok() : Error(StatusCodes.Status400BadRequest, npResult.Message ?? "Submission failed.");
        }

        // "single" or "import": persist the listens.
        var items = new List<ScrobbleRequest>(body.Payload.Count);
        foreach (var listen in body.Payload)
        {
            var meta = listen.TrackMetadata;
            if (string.IsNullOrWhiteSpace(meta?.ArtistName) || string.IsNullOrWhiteSpace(meta.TrackName))
                return Error(StatusCodes.Status400BadRequest, "Each listen's track_metadata must include artist_name and track_name.");

            items.Add(new ScrobbleRequest(meta.ArtistName!.Trim(), meta.TrackName!.Trim(), NullIfBlank(meta.ReleaseName), listen.ListenedAt));
        }

        var result = await scrobbles.SubmitBatchAsync(user.Id, new ScrobbleBatchRequest(items), ctx.RequestAborted);
        return result.Succeeded ? Ok() : Error(StatusCodes.Status400BadRequest, result.Message ?? "Submission failed.");
    }

    // GET /1/validate-token
    private static async Task<IResult> ValidateTokenAsync(HttpContext ctx, IAuthService auth)
    {
        var token = ExtractToken(ctx.Request);
        if (token is null)
            return Error(StatusCodes.Status401Unauthorized, "You need to provide an Authorization token.");

        var user = await auth.AuthenticateTokenAsync(token, ctx.RequestAborted);
        return user is null
            ? Results.Json(new { code = 200, message = "Token invalid.", valid = false })
            : Results.Json(new { code = 200, message = "Token valid.", valid = true, user_name = user.Username });
    }

    // GET /1/user/{userName}/listens
    private static async Task<IResult> GetListensAsync(
        string userName, int? count, HttpContext ctx, IAuthService auth, IScrobbleService scrobbles)
    {
        // A token is optional here; when present it lets the owner read their own private listens.
        var viewerUser = await ResolveUserAsync(ctx, auth);
        var viewer = viewerUser is null ? ViewerContext.Anonymous : new ViewerContext(viewerUser.Id, viewerUser.IsAdmin);

        var pageSize = Math.Clamp(count ?? 25, 1, 100);
        var result = await scrobbles.GetRecentAsync(userName, 1, pageSize, viewer, ctx.RequestAborted);
        if (result.Failed)
        {
            var status = result.Error == ResultError.Forbidden ? StatusCodes.Status403Forbidden : StatusCodes.Status404NotFound;
            return Error(status, result.Message ?? "Not found.");
        }

        var scrobbleItems = result.Value!.Items;
        var listens = scrobbleItems.Select(s => new
        {
            user_name = userName,
            listened_at = s.Timestamp,
            track_metadata = new
            {
                artist_name = s.Artist,
                track_name = s.Track,
                release_name = s.Album
            }
        }).ToList();

        return Results.Json(new
        {
            payload = new
            {
                count = listens.Count,
                user_id = userName,
                latest_listen_ts = scrobbleItems.Count > 0 ? scrobbleItems[0].Timestamp : 0L,
                listens
            }
        });
    }

    // GET /1/user/{userName}/playing-now
    private static async Task<IResult> GetPlayingNowAsync(string userName, HttpContext ctx, IScrobbleService scrobbles)
    {
        var np = await scrobbles.GetNowPlayingByUsernameAsync(userName, ctx.RequestAborted);
        var listens = np is null
            ? Array.Empty<object>()
            : new object[]
            {
                new
                {
                    track_metadata = new { artist_name = np.Artist, track_name = np.Track, release_name = np.Album },
                    playing_now = true
                }
            };

        return Results.Json(new
        {
            payload = new
            {
                count = listens.Length,
                user_id = userName,
                playing_now = true,
                listens
            }
        });
    }

    private static async Task<User?> ResolveUserAsync(HttpContext ctx, IAuthService auth)
    {
        var token = ExtractToken(ctx.Request);
        return token is null ? null : await auth.AuthenticateTokenAsync(token, ctx.RequestAborted);
    }

    /// <summary>Reads the token from the <c>Authorization: Token …</c> header or a <c>?token=</c> query value.</summary>
    private static string? ExtractToken(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Authorization", out var header))
        {
            var raw = header.ToString();
            if (raw.StartsWith(TokenAuthenticationDefaults.HeaderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = raw[TokenAuthenticationDefaults.HeaderPrefix.Length..].Trim();
                if (value.Length > 0) return value;
            }
        }

        if (request.Query.TryGetValue("token", out var query) && !string.IsNullOrWhiteSpace(query))
            return query.ToString().Trim();

        return null;
    }

    private static IResult Ok() => Results.Json(new { status = "ok" });

    private static IResult Error(int code, string error) => Results.Json(new { code, error }, statusCode: code);

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
