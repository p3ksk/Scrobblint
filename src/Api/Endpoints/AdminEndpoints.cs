using Scrobblint.Api.Authentication;
using Scrobblint.Api.Common;
using Scrobblint.Application.Common;
using Scrobblint.Application.Services;

namespace Scrobblint.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/admin/users")
            .WithTags("Admin")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        group.MapGet("", async (
            int? page, int? pageSize, string? search, IUserService users, CancellationToken ct) =>
        {
            var result = await users.GetUsersAsync(page ?? 1, pageSize ?? AppConstants.DefaultPageSize, search, ct);
            return result.ToHttpResult();
        })
        .WithName("AdminListUsers")
        .WithSummary("List users with scrobble counts (paged).");

        group.MapGet("/{id:guid}", async (Guid id, IUserService users, CancellationToken ct) =>
        {
            var result = await users.GetUserDetailAsync(id, ct);
            return result.ToHttpResult();
        })
        .WithName("AdminUserDetail")
        .WithSummary("Detailed information for a single user.");

        group.MapPost("/{id:guid}/disable", async (Guid id, IUserService users, CancellationToken ct) =>
        {
            var result = await users.SetDisabledAsync(id, true, ct);
            return result.ToHttpResult(StatusCodes.Status204NoContent);
        })
        .WithName("AdminDisableUser")
        .WithSummary("Disable a user account.");

        group.MapPost("/{id:guid}/enable", async (Guid id, IUserService users, CancellationToken ct) =>
        {
            var result = await users.SetDisabledAsync(id, false, ct);
            return result.ToHttpResult(StatusCodes.Status204NoContent);
        })
        .WithName("AdminEnableUser")
        .WithSummary("Re-enable a disabled user account.");

        group.MapPost("/{id:guid}/token", async (Guid id, IUserService users, CancellationToken ct) =>
        {
            var result = await users.RegenerateUserTokenAsync(id, ct);
            return result.ToHttpResult();
        })
        .WithName("AdminRegenerateToken")
        .WithSummary("Regenerate a user's API token.");

        group.MapDelete("/{id:guid}", async (Guid id, IUserService users, CancellationToken ct) =>
        {
            var result = await users.DeleteUserAsync(id, ct);
            return result.ToHttpResult(StatusCodes.Status204NoContent);
        })
        .WithName("AdminDeleteUser")
        .WithSummary("Permanently delete a user and all their data.");

        return api;
    }
}
