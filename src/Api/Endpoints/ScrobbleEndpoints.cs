using System.Security.Claims;
using Scrobblint.Api.Authentication;
using Scrobblint.Api.Common;
using Scrobblint.Application.Services;
using Scrobblint.Shared.Scrobbles;

namespace Scrobblint.Api.Endpoints;

public static class ScrobbleEndpoints
{
    public static RouteGroupBuilder MapScrobbleEndpoints(this RouteGroupBuilder api)
    {
        // Single scrobble: POST /api/scrobble
        api.MapPost("/scrobble", async (
            ScrobbleRequest request, ClaimsPrincipal user, IScrobbleService scrobbles, CancellationToken ct) =>
        {
            var result = await scrobbles.SubmitAsync(user.GetUserId()!.Value, request, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TokenAuthenticated)
        .WithTags("Scrobbles")
        .WithName("SubmitScrobble")
        .WithSummary("Submit a single listen.");

        // Batch: POST /api/scrobbles
        api.MapPost("/scrobbles", async (
            ScrobbleBatchRequest request, ClaimsPrincipal user, IScrobbleService scrobbles, CancellationToken ct) =>
        {
            var result = await scrobbles.SubmitBatchAsync(user.GetUserId()!.Value, request, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TokenAuthenticated)
        .WithTags("Scrobbles")
        .WithName("SubmitScrobbles")
        .WithSummary("Submit a batch of listens.");

        // Now-playing: PUT /api/now-playing
        api.MapPut("/now-playing", async (
            NowPlayingRequest request, ClaimsPrincipal user, IScrobbleService scrobbles, CancellationToken ct) =>
        {
            var result = await scrobbles.UpdateNowPlayingAsync(user.GetUserId()!.Value, request, ct);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .RequireAuthorization(AuthorizationPolicies.TokenAuthenticated)
        .WithTags("Scrobbles")
        .WithName("UpdateNowPlaying")
        .WithSummary("Notify external services of the track currently being played.");

        // Now-playing read: GET /api/now-playing
        api.MapGet("/now-playing", (ClaimsPrincipal user, IScrobbleService scrobbles) =>
        {
            var np = scrobbles.GetNowPlaying(user.GetUserId()!.Value);
            return np is not null ? Results.Ok(np) : Results.NoContent();
        })
        .RequireAuthorization(AuthorizationPolicies.TokenAuthenticated)
        .WithTags("Scrobbles")
        .WithName("GetNowPlaying")
        .WithSummary("Get the user's current now-playing track, if any.");

        return api;
    }
}
