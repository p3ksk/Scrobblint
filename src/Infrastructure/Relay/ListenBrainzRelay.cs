using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Scrobblint.Application.Abstractions.Relay;
using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;

namespace Scrobblint.Infrastructure.Relay;

/// <summary>
/// Relays listens to ListenBrainz (or a self-hosted compatible instance) via the
/// <c>submit-listens</c> API, authenticated with the user's token.
/// </summary>
public sealed class ListenBrainzRelay : IListenBrainzRelay
{
    public const string DefaultApiRoot = "https://api.listenbrainz.org";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ListenBrainzRelay> _logger;

    public ListenBrainzRelay(IHttpClientFactory httpClientFactory, ILogger<ListenBrainzRelay> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public ScrobbleProvider Provider => ScrobbleProvider.ListenBrainz;

    // No server-side configuration needed: users bring their own token.
    public bool IsConfigured => true;

    public async Task<RelayAuthResult> ValidateTokenAsync(string token, string? apiRoot, CancellationToken cancellationToken = default)
    {
        var root = Root(apiRoot);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{root}/1/validate-token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", token);

        try
        {
            using var response = await Client().SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return RelayAuthResult.Fail($"ListenBrainz rejected the token (HTTP {(int)response.StatusCode}).");

            using var doc = JsonDocument.Parse(json);
            var valid = doc.RootElement.TryGetProperty("valid", out var v) && v.ValueKind == JsonValueKind.True;
            if (!valid)
                return RelayAuthResult.Fail("That ListenBrainz token is not valid.");

            var userName = doc.RootElement.TryGetProperty("user_name", out var u) ? u.GetString() : null;
            return RelayAuthResult.Ok(token, userName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ListenBrainz token validation failed");
            return RelayAuthResult.Fail("Could not reach ListenBrainz to validate the token.");
        }
    }

    public async Task<RelayResult> SendAsync(ExternalConnection connection, IReadOnlyList<RelayTrack> tracks, CancellationToken cancellationToken = default)
    {
        if (tracks.Count == 0) return RelayResult.Ok(0);

        var root = Root(connection.ApiRoot);
        var payload = tracks.Select(t => new
        {
            listened_at = t.ListenedAtUnix,
            track_metadata = new
            {
                artist_name = t.Artist,
                track_name = t.Track,
                release_name = string.IsNullOrWhiteSpace(t.Album) ? null : t.Album
            }
        }).ToArray();

        var body = new
        {
            listen_type = tracks.Count == 1 ? "single" : "import",
            payload
        };

        // Serialise to a buffered StringContent (not JsonContent): this sends an explicit
        // Content-Length instead of chunked transfer-encoding. ListenBrainz's front end does not
        // accept a chunked request body and would otherwise see a zero-length document.
        var json = JsonSerializer.Serialize(body, SerializerOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{root}/1/submit-listens")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", connection.Token);

        using var response = await Client().SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return RelayResult.Ok(tracks.Count);

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return RelayResult.Fail($"ListenBrainz HTTP {(int)response.StatusCode}: {Truncate(error)}");
    }

    public async Task<RelayResult> SendNowPlayingAsync(ExternalConnection connection, string artist, string track, string? album, CancellationToken cancellationToken = default)
    {
        var root = Root(connection.ApiRoot);
        var body = new
        {
            listen_type = "playing_now",
            payload = new[]
            {
                new
                {
                    track_metadata = new
                    {
                        artist_name = artist,
                        track_name = track,
                        release_name = string.IsNullOrWhiteSpace(album) ? null : album
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(body, SerializerOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{root}/1/submit-listens")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", connection.Token);

        using var response = await Client().SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return RelayResult.Ok(1);

        var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
        return RelayResult.Fail($"ListenBrainz HTTP {(int)response.StatusCode}: {Truncate(errorText)}");
    }

    private static string Root(string? apiRoot) =>
        string.IsNullOrWhiteSpace(apiRoot) ? DefaultApiRoot : apiRoot.TrimEnd('/');

    private HttpClient Client() => _httpClientFactory.CreateClient(RelayHttpClient.Name);

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200];
}
