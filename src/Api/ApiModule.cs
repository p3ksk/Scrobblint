using Scrobblint.Api.Endpoints;

namespace Scrobblint.Api;

/// <summary>
/// Maps the whole Scrobblint REST surface under <c>/api</c>. Exposed as an extension so it can be
/// hosted by the standalone API project <em>and</em> reused in-process by the Blazor Web app.
/// </summary>
public static class ApiModule
{
    public static IEndpointRouteBuilder MapScrobblintApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapAuthEndpoints();
        api.MapScrobbleEndpoints();
        api.MapUserEndpoints();
        api.MapConnectionEndpoints();
        api.MapAdminEndpoints();
        api.MapGlobalStatsEndpoint();

        return app;
    }
}
