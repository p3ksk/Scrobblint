using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Scrobblint.Api.Authentication;
using Scrobblint.Application.Services;
using Scrobblint.Domain.Enums;
using Scrobblint.Shared.Users;

namespace Scrobblint.Web.Authentication;

/// <summary>
/// Cookie-authorized form-post endpoints for the authenticated UI (token regeneration, settings)
/// and admin actions. Kept separate from the token-authenticated REST API in <c>ApiModule</c>.
/// </summary>
public static class UiFormEndpoints
{
    public static IEndpointRouteBuilder MapUiFormEndpoints(this IEndpointRouteBuilder app)
    {
        var adminPolicy = new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme)
            .RequireRole(TokenAuthenticationDefaults.AdminRole)
            .Build();

        // ---- Current user ----
        app.MapPost("/account/token/regenerate", async (
            HttpContext context, IAntiforgery antiforgery, IAuthService auth) =>
        {
            if (!await Valid(antiforgery, context)) return Results.BadRequest();
            await auth.RegenerateTokenAsync(context.User.GetUserId()!.Value, context.RequestAborted);
            return Results.LocalRedirect("/token");
        }).RequireAuthorization();

        app.MapPost("/account/settings", async (
            HttpContext context, IAntiforgery antiforgery, IUserService users) =>
        {
            if (!await Valid(antiforgery, context)) return Results.BadRequest();
            var form = await context.Request.ReadFormAsync();
            var visibility = Enum.TryParse<ProfileVisibility>(form["visibility"], out var v) ? v : ProfileVisibility.Public;
            var theme = Enum.TryParse<Theme>(form["theme"], out var t) ? t : Theme.System;

            await users.UpdateSettingsAsync(context.User.GetUserId()!.Value, new UserSettingsDto(visibility, theme), context.RequestAborted);
            return Results.LocalRedirect("/settings?saved=1");
        }).RequireAuthorization();

        // ---- Admin ----
        var admin = app.MapGroup("/admin/users").RequireAuthorization(adminPolicy);

        admin.MapPost("/{id:guid}/disable", async (Guid id, HttpContext ctx, IAntiforgery af, IUserService users) =>
        {
            if (!await Valid(af, ctx)) return Results.BadRequest();
            await users.SetDisabledAsync(id, true, ctx.RequestAborted);
            return Results.LocalRedirect($"/admin/users/{id}");
        });

        admin.MapPost("/{id:guid}/enable", async (Guid id, HttpContext ctx, IAntiforgery af, IUserService users) =>
        {
            if (!await Valid(af, ctx)) return Results.BadRequest();
            await users.SetDisabledAsync(id, false, ctx.RequestAborted);
            return Results.LocalRedirect($"/admin/users/{id}");
        });

        admin.MapPost("/{id:guid}/token", async (Guid id, HttpContext ctx, IAntiforgery af, IUserService users) =>
        {
            if (!await Valid(af, ctx)) return Results.BadRequest();
            await users.RegenerateUserTokenAsync(id, ctx.RequestAborted);
            return Results.LocalRedirect($"/admin/users/{id}?tokenReset=1");
        });

        return app;
    }

    private static async Task<bool> Valid(IAntiforgery antiforgery, HttpContext context)
    {
        try { await antiforgery.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
}
