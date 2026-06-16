using System.Security.Claims;
using Scrobblint.Api.Authentication;
using Scrobblint.Api.Common;
using Scrobblint.Application.Services;
using Scrobblint.Shared.Auth;

namespace Scrobblint.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth").WithTags("Authentication");

        group.MapPost("/register", async (
            RegisterRequest request, IAuthService auth, CancellationToken ct) =>
        {
            var result = await auth.RegisterAsync(request, ct);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingPolicies.Auth)
        .WithName("Register")
        .WithSummary("Create a new account and receive its first API token.");

        group.MapPost("/login", async (
            LoginRequest request, IAuthService auth, CancellationToken ct) =>
        {
            var result = await auth.LoginAsync(request, ct);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingPolicies.Auth)
        .WithName("Login")
        .WithSummary("Validate credentials and return the account's API token.");

        group.MapPost("/token", async (
            ClaimsPrincipal user, IAuthService auth, CancellationToken ct) =>
        {
            var result = await auth.RegenerateTokenAsync(user.GetUserId()!.Value, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.TokenAuthenticated)
        .WithName("RegenerateToken")
        .WithSummary("Generate a fresh API token, invalidating the previous one.");

        return api;
    }
}
