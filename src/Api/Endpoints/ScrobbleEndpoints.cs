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

        return api;
    }
}
