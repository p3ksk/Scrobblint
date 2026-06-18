using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Scrobblint.Application.Abstractions.CoverArt;
using Scrobblint.Application.Common;

namespace Scrobblint.Infrastructure.CoverArt;

public sealed class DeezerCoverArtProvider : ICoverArtProvider
{
    public const string HttpClientName = "DeezerApi";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FifoCache _cache;
    private readonly ILogger<DeezerCoverArtProvider> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public DeezerCoverArtProvider(IHttpClientFactory httpClientFactory, ILogger<DeezerCoverArtProvider> logger, int maxEntries = 1000)
    {
        _httpClientFactory = httpClientFactory;
        _cache = new FifoCache(maxEntries);
        _logger = logger;
    }

    private HttpClient Client() => _httpClientFactory.CreateClient(HttpClientName);

    public Task<string?> GetArtistImageUrlAsync(string artist, CancellationToken ct = default)
    {
        var key = CacheKeys.Artwork("artist", artist);
        return _cache.GetOrAddAsync(key, () => ResolveArtistAsync(artist, ct));
    }

    public Task<string?> GetAlbumCoverUrlAsync(string artist, string album, CancellationToken ct = default)
    {
        var key = CacheKeys.Artwork("album", artist, album);
        return _cache.GetOrAddAsync(key, () => ResolveAlbumAsync(artist, album, ct));
    }

    private async Task<string?> ResolveArtistAsync(string artist, CancellationToken ct)
    {
        var imageUrl = await LookupArtistUrlAsync(artist, ct);
        if (imageUrl is null) return null;
        return await DownloadAsDataUriAsync(imageUrl, ct);
    }

    private async Task<string?> ResolveAlbumAsync(string artist, string album, CancellationToken ct)
    {
        var imageUrl = await LookupAlbumUrlAsync(artist, album, ct);
        if (imageUrl is null) return null;
        return await DownloadAsDataUriAsync(imageUrl, ct);
    }

    private async Task<string?> LookupArtistUrlAsync(string artist, CancellationToken ct)
    {
        try
        {
            var encoded = Uri.EscapeDataString(artist);
            var response = await Client().GetAsync($"search/artist?q={encoded}", ct);
            response.EnsureSuccessStatusCode();

            var result = await JsonSerializer.DeserializeAsync<DeezerSearchResult<DeezerArtist>>(
                await response.Content.ReadAsStreamAsync(ct),
                SerializerOptions,
                ct);

            if (result?.Data is { Count: > 0 } data && !string.IsNullOrEmpty(data[0].PictureSmall))
            {
                _logger.LogDebug("Found Deezer artist image for {Artist}", artist);
                return data[0].PictureSmall;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Deezer artist lookup failed for {Artist}", artist);
        }

        return null;
    }

    private async Task<string?> LookupAlbumUrlAsync(string artist, string album, CancellationToken ct)
    {
        try
        {
            var encoded = Uri.EscapeDataString($"artist:\"{artist}\" album:\"{album}\"");
            var response = await Client().GetAsync($"search/album?q={encoded}", ct);
            response.EnsureSuccessStatusCode();

            var result = await JsonSerializer.DeserializeAsync<DeezerSearchResult<DeezerAlbum>>(
                await response.Content.ReadAsStreamAsync(ct),
                SerializerOptions,
                ct);

            if (result?.Data is { Count: > 0 } data && !string.IsNullOrEmpty(data[0].CoverSmall))
            {
                _logger.LogDebug("Found Deezer album cover for {Artist} — {Album}", artist, album);
                return data[0].CoverSmall;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Deezer album lookup failed for {Artist} — {Album}", artist, album);
        }

        return null;
    }

    private async Task<string?> DownloadAsDataUriAsync(string imageUrl, CancellationToken ct)
    {
        try
        {
            // Use a fresh HttpClient (no BaseAddress) for the CDN download.
            using var cdnClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var bytes = await cdnClient.GetByteArrayAsync(imageUrl, ct);
            var base64 = Convert.ToBase64String(bytes);
            // Deezer CDN always serves JPEG.
            return $"data:image/jpeg;base64,{base64}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to download artwork from {Url}", imageUrl);
            return null;
        }
    }

    /// <summary>
    /// Size-limited string-keyed cache with FIFO eviction. Concurrent lookups for the same key are
    /// deduplicated — all callers await the same in-flight task. Null results are stored as
    /// completed tasks to avoid repeated failed lookups.
    /// </summary>
    private sealed class FifoCache
    {
        private readonly int _maxEntries;
        private readonly ConcurrentDictionary<string, Task<string?>> _entries = new();
        private readonly ConcurrentQueue<string> _order = new();
        private readonly object _evictLock = new();

        public FifoCache(int maxEntries) => _maxEntries = maxEntries;

        public Task<string?> GetOrAddAsync(string key, Func<Task<string?>> factory)
        {
            if (_entries.TryGetValue(key, out var existing))
                return existing;

            var newTask = CaptureAsync(key, factory);
            var winner = _entries.GetOrAdd(key, newTask);

            if (winner == newTask)
            {
                _order.Enqueue(key);
                EvictIfNeeded();
            }

            return winner;
        }

        private async Task<string?> CaptureAsync(string key, Func<Task<string?>> factory)
        {
            try { return await factory(); }
            catch { return null; }
        }

        private void EvictIfNeeded()
        {
            lock (_evictLock)
            {
                while (_order.Count > _maxEntries)
                {
                    if (_order.TryDequeue(out var oldest))
                        _entries.TryRemove(oldest, out _);
                }
            }
        }
    }
}
