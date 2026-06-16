using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Scrobblint.Shared.Auth;
using Scrobblint.Shared.Connections;
using Xunit;

namespace Scrobblint.IntegrationTests;

public class ConnectionEndpointTests : IClassFixture<ScrobblintApiFactory>
{
    private readonly ScrobblintApiFactory _factory;

    public ConnectionEndpointTests(ScrobblintApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthedClientAsync(string username)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(username, $"{username}@example.com", "supersecret"));
        var body = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", body!.Token);
        return client;
    }

    [Fact]
    public async Task Connections_require_authentication()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/connections");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task New_user_has_no_connections_and_reports_provider_availability()
    {
        var client = await AuthedClientAsync("freshuser");

        var connections = await client.GetFromJsonAsync<ConnectionsResponse>("/api/connections");

        Assert.NotNull(connections);
        Assert.Empty(connections!.Connections);
        Assert.True(connections.ListenBrainzAvailable);   // always available (user brings token)
        Assert.False(connections.LastfmAvailable);        // no API key/secret configured in tests
    }

    [Fact]
    public async Task Beginning_lastfm_auth_when_unconfigured_fails_cleanly()
    {
        var client = await AuthedClientAsync("fmuser");

        var resp = await client.PostAsJsonAsync("/api/connections/lastfm/begin",
            new BeginLastfmAuthRequest("https://example.com/callback"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
