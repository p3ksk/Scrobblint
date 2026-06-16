using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrobblint.Application.Abstractions.Relay;
using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;
using Scrobblint.Infrastructure.Configuration;

namespace Scrobblint.Infrastructure.Relay;

/// <summary>
/// Relays listens to Last.fm. Uses the app's API key/secret (server config) plus the user's session
/// key. The session key is obtained once via <c>auth.getMobileSession</c>; the password is never stored.
/// </summary>
public sealed class LastfmRelay : ILastfmRelay
{
    private const int MaxBatch = 50; // Last.fm accepts up to 50 scrobbles per request.

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LastfmOptions _options;
    private readonly ILogger<LastfmRelay> _logger;

    public LastfmRelay(IHttpClientFactory httpClientFactory, IOptions<LastfmOptions> options, ILogger<LastfmRelay> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public ScrobbleProvider Provider => ScrobbleProvider.Lastfm;

    public bool IsConfigured => _options.IsConfigured;

    public string BuildAuthorizeUrl(string callbackUrl)
    {
        // The user is sent here to approve access; Last.fm then redirects to the callback with ?token=.
        return $"https://www.last.fm/api/auth/?api_key={Uri.EscapeDataString(_options.ApiKey!)}" +
               $"&cb={Uri.EscapeDataString(callbackUrl)}";
    }

    public async Task<RelayAuthResult> CompleteAuthorizationAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return RelayAuthResult.Fail("Last.fm is not configured on this server.");
        if (string.IsNullOrWhiteSpace(token))
            return RelayAuthResult.Fail("Missing Last.fm authorization token.");

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["method"] = "auth.getSession",
            ["api_key"] = _options.ApiKey!,
            ["token"] = token
        };
        Sign(parameters);

        try
        {
            using var doc = await GetAsync(parameters, cancellationToken);
            if (TryGetError(doc.RootElement, out var error))
                return RelayAuthResult.Fail($"Last.fm: {error}");

            if (doc.RootElement.TryGetProperty("session", out var session) &&
                session.TryGetProperty("key", out var key))
            {
                var name = session.TryGetProperty("name", out var n) ? n.GetString() : null;
                return RelayAuthResult.Ok(key.GetString()!, name);
            }

            return RelayAuthResult.Fail("Last.fm did not return a session key.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Last.fm session exchange failed");
            return RelayAuthResult.Fail("Could not reach Last.fm to complete authorization.");
        }
    }

    public async Task<RelayResult> SendAsync(ExternalConnection connection, IReadOnlyList<RelayTrack> tracks, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return RelayResult.Fail("Last.fm is not configured.");
        if (tracks.Count == 0) return RelayResult.Ok(0);

        var accepted = 0;
        foreach (var chunk in Chunk(tracks, MaxBatch))
        {
            var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["method"] = "track.scrobble",
                ["api_key"] = _options.ApiKey!,
                ["sk"] = connection.Token
            };

            for (var i = 0; i < chunk.Count; i++)
            {
                parameters[$"artist[{i}]"] = chunk[i].Artist;
                parameters[$"track[{i}]"] = chunk[i].Track;
                parameters[$"timestamp[{i}]"] = chunk[i].ListenedAtUnix.ToString();
                if (!string.IsNullOrWhiteSpace(chunk[i].Album))
                    parameters[$"album[{i}]"] = chunk[i].Album!;
            }
            Sign(parameters);

            using var doc = await PostAsync(parameters, cancellationToken);
            if (TryGetError(doc.RootElement, out var error))
                return RelayResult.Fail($"Last.fm: {error}");

            accepted += CountAccepted(doc.RootElement, chunk.Count);
        }

        return RelayResult.Ok(accepted);
    }

    public async Task<RelayHistoryResult> GetRecentTracksAsync(
        string username, int page, int limit, long? toUnix, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return RelayHistoryResult.Fail("Last.fm is not configured.");

        // user.getRecentTracks is an unsigned public read — no api_sig / session key required.
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["method"] = "user.getRecentTracks",
            ["user"] = username,
            ["api_key"] = _options.ApiKey!,
            ["limit"] = limit.ToString(),
            ["page"] = page.ToString()
        };
        if (toUnix is > 0) parameters["to"] = toUnix.Value.ToString();

        try
        {
            using var doc = await GetAsync(parameters, cancellationToken);
            if (TryGetError(doc.RootElement, out var error))
                return RelayHistoryResult.Fail($"Last.fm: {error}");
            if (!doc.RootElement.TryGetProperty("recenttracks", out var rt))
                return RelayHistoryResult.Fail("Unexpected Last.fm response.");

            var totalPages = page;
            var total = 0;
            var currentPage = page;
            if (rt.TryGetProperty("@attr", out var attr))
            {
                totalPages = ParseInt(attr, "totalPages", page);
                total = ParseInt(attr, "total", 0);
                currentPage = ParseInt(attr, "page", page);
            }

            var tracks = new List<RelayTrack>();
            if (rt.TryGetProperty("track", out var trackEl))
            {
                if (trackEl.ValueKind == JsonValueKind.Array)
                    foreach (var t in trackEl.EnumerateArray()) AddTrack(t, tracks);
                else if (trackEl.ValueKind == JsonValueKind.Object)
                    AddTrack(trackEl, tracks);
            }

            return RelayHistoryResult.Ok(new RelayHistoryPage(tracks, currentPage, totalPages, total));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Last.fm getRecentTracks failed for {User} page {Page}", username, page);
            return RelayHistoryResult.Fail("Could not reach Last.fm to fetch history.");
        }
    }

    private static void AddTrack(JsonElement track, List<RelayTrack> into)
    {
        // Skip the "now playing" entry (it has no listen time).
        if (track.TryGetProperty("@attr", out var a) &&
            a.TryGetProperty("nowplaying", out var np) && np.GetString() == "true")
            return;
        if (!track.TryGetProperty("date", out var date) || !date.TryGetProperty("uts", out var uts))
            return;
        if (!long.TryParse(uts.GetString(), out var unix) || unix <= 0)
            return;

        var artist = GetText(track, "artist");
        var name = track.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
        var album = GetText(track, "album");
        if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(name))
            return;

        into.Add(new RelayTrack(artist, name, string.IsNullOrWhiteSpace(album) ? null : album, unix));
    }

    private static string GetText(JsonElement parent, string property)
    {
        if (parent.TryGetProperty(property, out var el))
        {
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("#text", out var txt)) return txt.GetString() ?? string.Empty;
            if (el.ValueKind == JsonValueKind.String) return el.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static int ParseInt(JsonElement parent, string property, int fallback)
    {
        if (parent.TryGetProperty(property, out var el))
        {
            if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var s)) return s;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
        }
        return fallback;
    }

    // ---- helpers ----

    private void Sign(SortedDictionary<string, string> parameters)
    {
        // api_sig = md5( concat(sorted key+value, excluding 'format'/'callback') + secret )
        var sb = new StringBuilder();
        foreach (var kvp in parameters)
        {
            if (kvp.Key is "format" or "callback") continue;
            sb.Append(kvp.Key).Append(kvp.Value);
        }
        sb.Append(_options.ApiSecret);

        var hash = MD5.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        parameters["api_sig"] = Convert.ToHexStringLower(hash);
    }

    private async Task<JsonDocument> PostAsync(IDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>(parameters) { ["format"] = "json" };
        using var content = new FormUrlEncodedContent(form);
        var client = _httpClientFactory.CreateClient(RelayHttpClient.Name);
        using var response = await client.PostAsync(_options.ApiRoot, content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
    }

    private async Task<JsonDocument> GetAsync(IDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var query = string.Join("&", parameters
            .Append(new KeyValuePair<string, string>("format", "json"))
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        var url = $"{_options.ApiRoot}?{query}";
        var client = _httpClientFactory.CreateClient(RelayHttpClient.Name);
        using var response = await client.GetAsync(url, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
    }

    private static bool TryGetError(JsonElement root, out string message)
    {
        if (root.TryGetProperty("error", out var err))
        {
            message = root.TryGetProperty("message", out var m) ? m.GetString() ?? "error" : $"error {err}";
            return true;
        }
        message = string.Empty;
        return false;
    }

    private static int CountAccepted(JsonElement root, int fallback)
    {
        if (root.TryGetProperty("scrobbles", out var s) &&
            s.TryGetProperty("@attr", out var attr) &&
            attr.TryGetProperty("accepted", out var a))
        {
            if (a.ValueKind == JsonValueKind.Number && a.TryGetInt32(out var n)) return n;
            if (a.ValueKind == JsonValueKind.String && int.TryParse(a.GetString(), out var ns)) return ns;
        }
        return fallback;
    }

    private static IEnumerable<IReadOnlyList<RelayTrack>> Chunk(IReadOnlyList<RelayTrack> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
            yield return source.Skip(i).Take(size).ToList();
    }
}
