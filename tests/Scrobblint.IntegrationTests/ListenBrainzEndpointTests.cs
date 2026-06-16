using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Scrobblint.Shared.Auth;
using Xunit;

namespace Scrobblint.IntegrationTests;

/// <summary>
/// Verifies the ListenBrainz-compatible surface at /1 so a ListenBrainz client works by changing
/// only its base URL.
/// </summary>
public class ListenBrainzEndpointTests : IClassFixture<ScrobblintApiFactory>
{
    private readonly ScrobblintApiFactory _factory;

    public ListenBrainzEndpointTests(ScrobblintApiFactory factory) => _factory = factory;

    private static void Authorize(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", token);

    private async Task<(HttpClient Client, string Token, string Username)> RegisterAsync(string username)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(username, $"{username}@example.com", "supersecret"));
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
        Authorize(client, body!.Token);
        return (client, body.Token, username);
    }

    [Fact]
    public async Task Validate_token_reports_valid_and_invalid()
    {
        var (client, token, username) = await RegisterAsync("lb_validate");

        var ok = await client.GetFromJsonAsync<JsonElement>("/1/validate-token");
        Assert.True(ok.GetProperty("valid").GetBoolean());
        Assert.Equal(username, ok.GetProperty("user_name").GetString());

        // Token can also be supplied as a query parameter (no Authorization header).
        var anon = _factory.CreateClient();
        var viaQuery = await anon.GetFromJsonAsync<JsonElement>($"/1/validate-token?token={token}");
        Assert.True(viaQuery.GetProperty("valid").GetBoolean());

        var bad = await anon.GetFromJsonAsync<JsonElement>("/1/validate-token?token=not-a-real-token");
        Assert.False(bad.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task Submit_listens_requires_a_token()
    {
        var anon = _factory.CreateClient();
        var body = new
        {
            listen_type = "single",
            payload = new[] { new { listened_at = 1700000000, track_metadata = new { artist_name = "A", track_name = "B" } } }
        };
        var resp = await anon.PostAsJsonAsync("/1/submit-listens", body);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Submit_single_listen_persists_and_reads_back()
    {
        var (client, _, username) = await RegisterAsync("lb_submit");

        var body = new
        {
            listen_type = "single",
            payload = new[]
            {
                new
                {
                    listened_at = 1700000000,
                    track_metadata = new { artist_name = "Radiohead", track_name = "Idioteque", release_name = "Kid A" }
                }
            }
        };

        var submit = await client.PostAsJsonAsync("/1/submit-listens", body);
        submit.EnsureSuccessStatusCode();
        var status = await submit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", status.GetProperty("status").GetString());

        var listens = await client.GetFromJsonAsync<JsonElement>($"/1/user/{username}/listens");
        var payload = listens.GetProperty("payload");
        Assert.Equal(1, payload.GetProperty("count").GetInt32());

        var first = payload.GetProperty("listens")[0];
        Assert.Equal(1700000000, first.GetProperty("listened_at").GetInt64());
        var meta = first.GetProperty("track_metadata");
        Assert.Equal("Radiohead", meta.GetProperty("artist_name").GetString());
        Assert.Equal("Idioteque", meta.GetProperty("track_name").GetString());
        Assert.Equal("Kid A", meta.GetProperty("release_name").GetString());
    }

    [Fact]
    public async Task Import_listen_type_accepts_multiple()
    {
        var (client, _, username) = await RegisterAsync("lb_import");

        var body = new
        {
            listen_type = "import",
            payload = new[]
            {
                new { listened_at = 1700000100, track_metadata = new { artist_name = "Boards of Canada", track_name = "Roygbiv" } },
                new { listened_at = 1700000200, track_metadata = new { artist_name = "Aphex Twin", track_name = "Xtal" } },
            }
        };

        var submit = await client.PostAsJsonAsync("/1/submit-listens", body);
        submit.EnsureSuccessStatusCode();

        var listens = await client.GetFromJsonAsync<JsonElement>($"/1/user/{username}/listens");
        Assert.Equal(2, listens.GetProperty("payload").GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Playing_now_is_reported_but_not_persisted()
    {
        var (client, _, username) = await RegisterAsync("lb_np");

        var body = new
        {
            listen_type = "playing_now",
            payload = new[] { new { track_metadata = new { artist_name = "Burial", track_name = "Archangel" } } }
        };

        var submit = await client.PostAsJsonAsync("/1/submit-listens", body);
        submit.EnsureSuccessStatusCode();

        var np = await client.GetFromJsonAsync<JsonElement>($"/1/user/{username}/playing-now");
        var payload = np.GetProperty("payload");
        Assert.Equal(1, payload.GetProperty("count").GetInt32());
        Assert.Equal("Burial", payload.GetProperty("listens")[0].GetProperty("track_metadata").GetProperty("artist_name").GetString());

        // playing_now must not be persisted as a listen.
        var listens = await client.GetFromJsonAsync<JsonElement>($"/1/user/{username}/listens");
        Assert.Equal(0, listens.GetProperty("payload").GetProperty("count").GetInt32());
    }
}
