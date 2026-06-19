using System.Security.Claims;
using Scrobblint.Api.Common;
using Scrobblint.Application.Common;
using Scrobblint.Application.Services;

namespace Scrobblint.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/user/{username}").WithTags("Users");

        group.MapGet("", async (
            string username, ClaimsPrincipal user, IUserService users, CancellationToken ct) =>
        {
            var result = await users.GetProfileAsync(username, ViewerContextFactory.From(user), ct);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("GetUserProfile")
        .WithSummary("Public profile for a user (respects profile visibility).");

        group.MapGet("/recent", async (
            string username, int? page, int? pageSize,
            ClaimsPrincipal user, IScrobbleService scrobbles, CancellationToken ct) =>
        {
            var result = await scrobbles.GetRecentAsync(
                username,
                page ?? 1,
                pageSize ?? AppConstants.DefaultPageSize,
                ViewerContextFactory.From(user),
                ct);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("GetRecentScrobbles")
        .WithSummary("Paged recent listens for a user.");

        group.MapGet("/stats", async (
            string username, ClaimsPrincipal user, IStatisticsService stats, CancellationToken ct) =>
        {
            var result = await stats.GetStatsAsync(username, ViewerContextFactory.From(user), cancellationToken: ct);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("GetUserStats")
        .WithSummary("Listening statistics for a user.");

        return api;
    }
}
