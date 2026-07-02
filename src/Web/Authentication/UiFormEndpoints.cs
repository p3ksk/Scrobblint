using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Scrobblint.Api.Authentication;
using Scrobblint.Application.Abstractions.Relay;
using Scrobblint.Application.Services;
using Scrobblint.Domain.Enums;
using Scrobblint.Shared.Connections;
using Scrobblint.Shared.Users;

namespace Scrobblint.Web.Authentication;

/// <summary>
/// Cookie-authorized form-post endpoints for the authenticated UI (token regeneration, settings)
/// and admin actions. Kept separate from the token-authenticated REST API in <c>ApiModule</c>.
/// </summary>
public static class UiFormEndpoints
{
    private const string LastfmStateCookie = "scrobblint.lastfm_state";

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
            return Results.LocalRedirect("/settings/token");
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

        // ---- External connections (relay to Last.fm / ListenBrainz) ----
        app.MapPost("/account/connections/listenbrainz", async (
            HttpContext context, IAntiforgery antiforgery, IExternalConnectionService svc) =>
        {
            if (!await Valid(antiforgery, context)) return Results.BadRequest();
            var form = await context.Request.ReadFormAsync();
            var request = new ConnectListenBrainzRequest(form["token"].ToString(), form["apiRoot"].ToString());
            var result = await svc.ConnectListenBrainzAsync(context.User.GetUserId()!.Value, request, context.RequestAborted);
            return RedirectConnections(result);
        }).RequireAuthorization();

        // Last.fm web-authorization flow: redirect the user to Last.fm, then exchange the returned
        // token for a session key on the callback. A state cookie guards against forged callbacks.
        app.MapPost("/account/connections/lastfm/start", async (
            HttpContext context, IAntiforgery antiforgery, IExternalConnectionService svc) =>
        {
            if (!await Valid(antiforgery, context)) return Results.BadRequest();

            var state = Guid.NewGuid().ToString("N");
            context.Response.Cookies.Append(LastfmStateCookie, state, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                MaxAge = TimeSpan.FromMinutes(10),
                Path = "/account/connections"
            });

            var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
            var callbackUrl = $"{baseUrl}/account/connections/lastfm/callback?state={state}";
            var result = svc.BeginLastfmAuth(callbackUrl);
            return result.Succeeded
                ? Results.Redirect(result.Value!)
                : Results.LocalRedirect($"/settings/connections?error={Uri.EscapeDataString(result.Message ?? "Failed.")}");
        }).RequireAuthorization();

        app.MapGet("/account/connections/lastfm/callback", async (
            HttpContext context, string? token, string? state, IExternalConnectionService svc) =>
        {
            var expected = context.Request.Cookies[LastfmStateCookie];
            context.Response.Cookies.Delete(LastfmStateCookie, new CookieOptions { Path = "/account/connections" });

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(state) || state != expected)
                return Results.LocalRedirect("/settings/connections?error=Last.fm+authorization+was+cancelled+or+invalid.");

            var result = await svc.CompleteLastfmAuthAsync(context.User.GetUserId()!.Value, token, context.RequestAborted);
            return RedirectConnections(result);
        }).RequireAuthorization();

        app.MapPost("/account/connections/{provider}/toggle", async (
            string provider, HttpContext context, IAntiforgery antiforgery, IExternalConnectionService svc) =>
        {
            if (!await Valid(antiforgery, context)) return Results.BadRequest();
            if (!Enum.TryParse<ScrobbleProvider>(provider, true, out var p)) return Results.NotFound();
            var enabled = context.Request.Form["enabled"] == "true";
            await svc.SetEnabledAsync(context.User.GetUserId()!.Value, p, enabled, context.RequestAborted);
            return Results.LocalRedirect("/settings/connections");
        }).RequireAuthorization();

        // Last.fm full-history import (background; progress shown on the Connections page).
        app.MapPost("/account/connections/lastfm/import", async (
            HttpContext context, IAntiforgery antiforgery, IScrobbleImportService imports) =>
        {
            if (!await Valid(antiforgery, context)) return Results.BadRequest();
            await imports.StartLastfmImportAsync(context.User.GetUserId()!.Value, context.RequestAborted);
            return Results.LocalRedirect("/settings/connections");
        }).RequireAuthorization();

        app.MapPost("/account/connections/lastfm/import/cancel", async (
            HttpContext context, IAntiforgery antiforgery, IScrobbleImportService imports) =>
        {
            if (!await Valid(antiforgery, context)) return Results.BadRequest();
            await imports.CancelAsync(context.User.GetUserId()!.Value, context.RequestAborted);
            return Results.LocalRedirect("/settings/connections");
        }).RequireAuthorization();

        app.MapPost("/account/connections/{provider}/disconnect", async (
            string provider, HttpContext context, IAntiforgery antiforgery, IExternalConnectionService svc) =>
        {
            if (!await Valid(antiforgery, context)) return Results.BadRequest();
            if (!Enum.TryParse<ScrobbleProvider>(provider, true, out var p)) return Results.NotFound();
            await svc.DisconnectAsync(context.User.GetUserId()!.Value, p, context.RequestAborted);
            return Results.LocalRedirect("/settings/connections");
        }).RequireAuthorization();

        // ---- Current user: scrobble management ----
        app.MapPost("/account/scrobbles/{id:guid}/delete", async (
            Guid id, HttpContext context, IAntiforgery antiforgery, IScrobbleService scrobbles) =>
        {
            if (!await Valid(antiforgery, context)) return Results.BadRequest();
            var result = await scrobbles.DeleteAsync(context.User.GetUserId()!.Value, id, context.RequestAborted);
            return result.Succeeded
                ? Results.LocalRedirect("/recent?deleted=1")
                : Results.LocalRedirect($"/recent?error={Uri.EscapeDataString(result.Message ?? "Failed.")}");
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

        admin.MapPost("/{id:guid}/delete", async (Guid id, HttpContext ctx, IAntiforgery af, IUserService users) =>
        {
            if (!await Valid(af, ctx)) return Results.BadRequest();
            var result = await users.DeleteUserAsync(id, ctx.RequestAborted);
            return result.Succeeded
                ? Results.LocalRedirect("/admin/users?deleted=1")
                : Results.LocalRedirect($"/admin/users/{id}?error={Uri.EscapeDataString(result.Message ?? "Failed.")}");
        });

        // ---- Admin: retry cache (failed relays) ----
        var relayRetries = app.MapGroup("/admin/relayretries").RequireAuthorization(adminPolicy);

        relayRetries.MapPost("/{id:guid}/retry", async (Guid id, HttpContext ctx, IAntiforgery af, IAdminService admin) =>
        {
            if (!await Valid(af, ctx)) return Results.BadRequest();
            var form = await ctx.Request.ReadFormAsync();
            await admin.RetryFailedRelayAsync(id, ctx.RequestAborted);
            return Results.LocalRedirect($"/admin/retrycache?page={RetryPage(form)}&retried=1");
        });

        relayRetries.MapPost("/{id:guid}/delete", async (Guid id, HttpContext ctx, IAntiforgery af, IAdminService admin) =>
        {
            if (!await Valid(af, ctx)) return Results.BadRequest();
            var form = await ctx.Request.ReadFormAsync();
            await admin.DeleteFailedRelayAsync(id, ctx.RequestAborted);
            return Results.LocalRedirect($"/admin/retrycache?page={RetryPage(form)}&deleted=1");
        });

        relayRetries.MapPost("/retry-all", async (HttpContext ctx, IAntiforgery af, IAdminService admin, IFailedRelayWorkerTrigger trigger) =>
        {
            if (!await Valid(af, ctx)) return Results.BadRequest();
            var form = await ctx.Request.ReadFormAsync();
            var result = await admin.RetryAllFailedAsync(ctx.RequestAborted);
            if (result.Succeeded) trigger.RequestRun();
            return Results.LocalRedirect($"/admin/retrycache?page={RetryPage(form)}&retriedAll={result.Value}");
        });

        relayRetries.MapPost("/run-now", async (HttpContext ctx, IAntiforgery af, IFailedRelayWorkerTrigger trigger) =>
        {
            if (!await Valid(af, ctx)) return Results.BadRequest();
            var form = await ctx.Request.ReadFormAsync();
            trigger.RequestRun();
            return Results.LocalRedirect($"/admin/retrycache?page={RetryPage(form)}&ranNow=1");
        });

        // ---- Current user: retry cache (own stuck relays) ----
        app.MapPost("/account/relayretries/{id:guid}/retry", async (
            Guid id, HttpContext context, IAntiforgery antiforgery, IExternalConnectionService svc) =>
        {
            if (!await Valid(antiforgery, context)) return Results.BadRequest();
            var result = await svc.RetryFailedRelayAsync(context.User.GetUserId()!.Value, id, context.RequestAborted);
            return result.Succeeded
                ? Results.LocalRedirect("/settings/connections?retried=1")
                : Results.LocalRedirect($"/settings/connections?error={Uri.EscapeDataString(result.Message ?? "Failed.")}");
        }).RequireAuthorization();

        app.MapPost("/account/relayretries/{id:guid}/delete", async (
            Guid id, HttpContext context, IAntiforgery antiforgery, IExternalConnectionService svc) =>
        {
            if (!await Valid(antiforgery, context)) return Results.BadRequest();
            var result = await svc.DeleteFailedRelayAsync(context.User.GetUserId()!.Value, id, context.RequestAborted);
            return result.Succeeded
                ? Results.LocalRedirect("/settings/connections?deleted=1")
                : Results.LocalRedirect($"/settings/connections?error={Uri.EscapeDataString(result.Message ?? "Failed.")}");
        }).RequireAuthorization();

        return app;
    }

    private static async Task<bool> Valid(IAntiforgery antiforgery, HttpContext context)
    {
        try { await antiforgery.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static int RetryPage(IFormCollection form) =>
        int.TryParse(form["page"], out var page) && page > 0 ? page : 1;

    private static IResult RedirectConnections(Scrobblint.Application.Common.Result result) =>
        result.Succeeded
            ? Results.LocalRedirect("/settings/connections?saved=1")
            : Results.LocalRedirect($"/settings/connections?error={Uri.EscapeDataString(result.Message ?? "Failed.")}");
}
