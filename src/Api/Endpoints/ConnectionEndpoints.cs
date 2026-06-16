using System.Security.Claims;
using Scrobblint.Api.Authentication;
using Scrobblint.Api.Common;
using Scrobblint.Application.Services;
using Scrobblint.Domain.Enums;
using Scrobblint.Shared.Connections;

namespace Scrobblint.Api.Endpoints;

public static class ConnectionEndpoints
{
    public static RouteGroupBuilder MapConnectionEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/connections")
            .WithTags("Connections")
            .RequireAuthorization(AuthorizationPolicies.TokenAuthenticated);

        group.MapGet("", async (ClaimsPrincipal user, IExternalConnectionService svc, CancellationToken ct) =>
        {
            var result = await svc.GetConnectionsAsync(user.GetUserId()!.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetConnections")
        .WithSummary("List the caller's linked external scrobbling services.");

        group.MapPost("/listenbrainz", async (
            ConnectListenBrainzRequest request, ClaimsPrincipal user, IExternalConnectionService svc, CancellationToken ct) =>
        {
            var result = await svc.ConnectListenBrainzAsync(user.GetUserId()!.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("ConnectListenBrainz")
        .WithSummary("Link a ListenBrainz account (token validated before saving).");

        group.MapPost("/lastfm/begin", (
            BeginLastfmAuthRequest request, IExternalConnectionService svc) =>
        {
            var result = svc.BeginLastfmAuth(request.CallbackUrl);
            return result.Succeeded
                ? Results.Ok(new LastfmAuthUrlResponse(result.Value!))
                : result.ToHttpResult();
        })
        .WithName("BeginLastfmAuth")
        .WithSummary("Start Last.fm web authorization; returns the URL to send the user to.");

        group.MapPost("/lastfm/complete", async (
            CompleteLastfmAuthRequest request, ClaimsPrincipal user, IExternalConnectionService svc, CancellationToken ct) =>
        {
            var result = await svc.CompleteLastfmAuthAsync(user.GetUserId()!.Value, request.Token, ct);
            return result.ToHttpResult();
        })
        .WithName("CompleteLastfmAuth")
        .WithSummary("Finish Last.fm linking with the token returned to your callback.");

        // ----- Last.fm history import -----
        group.MapPost("/lastfm/import", async (
            ClaimsPrincipal user, IScrobbleImportService imports, CancellationToken ct) =>
        {
            var result = await imports.StartLastfmImportAsync(user.GetUserId()!.Value, ct);
            return result.ToHttpResult(StatusCodes.Status202Accepted);
        })
        .WithName("StartLastfmImport")
        .WithSummary("Start importing the caller's full Last.fm scrobble history (runs in the background).");

        group.MapGet("/lastfm/import", async (
            ClaimsPrincipal user, IScrobbleImportService imports, CancellationToken ct) =>
        {
            var status = await imports.GetStatusAsync(user.GetUserId()!.Value, ct);
            return status is null ? Results.NoContent() : Results.Ok(status);
        })
        .WithName("GetLastfmImportStatus")
        .WithSummary("Progress of the caller's most recent history import.");

        group.MapDelete("/lastfm/import", async (
            ClaimsPrincipal user, IScrobbleImportService imports, CancellationToken ct) =>
        {
            var result = await imports.CancelAsync(user.GetUserId()!.Value, ct);
            return result.ToHttpResult(StatusCodes.Status204NoContent);
        })
        .WithName("CancelLastfmImport")
        .WithSummary("Cancel the caller's running history import.");

        group.MapPost("/{provider}/enabled", async (
            string provider, bool value, ClaimsPrincipal user, IExternalConnectionService svc, CancellationToken ct) =>
        {
            if (!TryParseProvider(provider, out var p))
                return Results.NotFound();
            var result = await svc.SetEnabledAsync(user.GetUserId()!.Value, p, value, ct);
            return result.ToHttpResult(StatusCodes.Status204NoContent);
        })
        .WithName("SetConnectionEnabled")
        .WithSummary("Enable or disable relaying to a linked service (?value=true|false).");

        group.MapDelete("/{provider}", async (
            string provider, ClaimsPrincipal user, IExternalConnectionService svc, CancellationToken ct) =>
        {
            if (!TryParseProvider(provider, out var p))
                return Results.NotFound();
            var result = await svc.DisconnectAsync(user.GetUserId()!.Value, p, ct);
            return result.ToHttpResult(StatusCodes.Status204NoContent);
        })
        .WithName("Disconnect")
        .WithSummary("Unlink an external service.");

        return api;
    }

    private static bool TryParseProvider(string value, out ScrobbleProvider provider) =>
        Enum.TryParse(value, ignoreCase: true, out provider) && Enum.IsDefined(provider);
}
