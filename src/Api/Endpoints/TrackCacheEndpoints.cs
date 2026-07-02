using Scrobblint.Api.Authentication;
using Scrobblint.Api.Common;
using Scrobblint.Application.Abstractions.Persistence;

namespace Scrobblint.Api.Endpoints;

public static class TrackCacheEndpoints
{
    public static RouteGroupBuilder MapTrackCacheEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/admin/trackcache")
            .WithTags("Admin")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        group.MapPost("/{id:guid}", async (
            Guid id,
            string? CanonicalArtist,
            string? CanonicalTrack,
            string? CanonicalAlbum,
            bool? Found,
            ITrackInfoRepository trackInfo,
            IUnitOfWork unitOfWork,
            CancellationToken ct) =>
        {
            var entry = await trackInfo.GetByIdAsync(id, ct);
            if (entry is null)
                return Results.NotFound();

            entry.CanonicalArtist = CanonicalArtist;
            entry.CanonicalTrack = CanonicalTrack;
            entry.CanonicalAlbum = CanonicalAlbum;
            if (Found.HasValue)
                entry.Found = Found.Value;

            trackInfo.Update(entry);
            await unitOfWork.SaveChangesAsync(ct);

            return Results.Redirect($"/admin/trackcache/{id}?saved=1");
        })
        .WithName("AdminUpdateTrackCache")
        .WithSummary("Update a track cache entry.");

        group.MapPost("/{id:guid}/delete", async (
            Guid id,
            ITrackInfoRepository trackInfo,
            IUnitOfWork unitOfWork,
            CancellationToken ct) =>
        {
            var entry = await trackInfo.GetByIdAsync(id, ct);
            if (entry is null)
                return Results.NotFound();

            trackInfo.Delete(entry);
            await unitOfWork.SaveChangesAsync(ct);

            return Results.Redirect("/admin/trackcache");
        })
        .WithName("AdminDeleteTrackCache")
        .WithSummary("Delete a track cache entry.");

        return api;
    }
}
