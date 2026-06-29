using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Abstractions.Pipeline;
using Scrobblint.Domain.Entities;
using Scrobblint.Infrastructure.Configuration;
using System.Text.Json;

namespace Scrobblint.Infrastructure.Pipeline;

/// <summary>
/// Stage 1: Enrichment pipeline worker.
/// Consumes raw scrobbles, enriches metadata via Last.fm (cached in the DB), forwards to save queue.
/// </summary>
public sealed class EnrichmentStageWorker : BackgroundService
{
    private readonly IScrobblePipelineQueue _inputQueue;
    private readonly ISaveQueue _outputQueue;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LastfmOptions _options;
    private readonly ILogger<EnrichmentStageWorker> _logger;

    public EnrichmentStageWorker(
        IScrobblePipelineQueue inputQueue,
        ISaveQueue outputQueue,
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<LastfmOptions> options,
        ILogger<EnrichmentStageWorker> logger)
    {
        _inputQueue = inputQueue;
        _outputQueue = outputQueue;
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Pipeline Stage 1 (Enrichment) started");

        await foreach (var scrobble in _inputQueue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                // Try to enrich metadata from Last.fm
                var enriched = await EnrichAsync(scrobble, stoppingToken);

                // Forward to save queue (Stage 2)
                _outputQueue.Enqueue(enriched);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in enrichment stage for {Artist} - {Track}", scrobble.Artist, scrobble.Track);

                // Forward original data on error (best effort)
                _outputQueue.Enqueue(scrobble);
            }
        }

        _logger.LogInformation("Pipeline Stage 1 (Enrichment) stopped");
    }

    private async Task<PipelineScrobble> EnrichAsync(PipelineScrobble scrobble, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogDebug("Last.fm not configured, skipping enrichment");
            return scrobble;
        }

        try
        {
            var metadata = await GetOrFetchMetadataAsync(scrobble, cancellationToken);
            if (metadata is null)
            {
                _logger.LogDebug("No metadata found for {Artist} - {Track}", scrobble.Artist, scrobble.Track);
                return scrobble;
            }

            var changes = new List<string>();
            var enrichedArtist = scrobble.Artist;
            var enrichedTrack = scrobble.Track;
            var enrichedAlbum = scrobble.Album;

            if (!string.IsNullOrWhiteSpace(metadata.Artist) && metadata.Artist != scrobble.Artist)
            {
                changes.Add($"artist: '{scrobble.Artist}' -> '{metadata.Artist}'");
                enrichedArtist = metadata.Artist;
            }

            if (!string.IsNullOrWhiteSpace(metadata.Track) && metadata.Track != scrobble.Track)
            {
                changes.Add($"track: '{scrobble.Track}' -> '{metadata.Track}'");
                enrichedTrack = metadata.Track;
            }

            // Only fill in album from Last.fm when the client didn't supply one.
            // Last.fm maps every track to a single canonical release, but the user's player
            // knows which album they're actually listening to — don't clobber that.
            if (!string.IsNullOrWhiteSpace(metadata.Album) && string.IsNullOrWhiteSpace(scrobble.Album))
            {
                changes.Add($"album: (empty) -> '{metadata.Album}'");
                enrichedAlbum = metadata.Album;
            }

            if (changes.Count > 0)
            {
                _logger.LogInformation("Enriched {Artist} - {Track}: {Changes}",
                    scrobble.Artist, scrobble.Track, string.Join(", ", changes));
            }

            return scrobble with
            {
                Artist = enrichedArtist,
                Track = enrichedTrack,
                Album = enrichedAlbum
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enrich {Artist} - {Track}", scrobble.Artist, scrobble.Track);
            return scrobble;
        }
    }

    /// <summary>
    /// Returns track metadata from the DB cache, falling back to the Last.fm API on a miss and caching
    /// the result (including "not found" misses, so the same unknown track is never re-queried).
    /// </summary>
    private async Task<TrackMetadata?> GetOrFetchMetadataAsync(PipelineScrobble scrobble, CancellationToken cancellationToken)
    {
        var artistKey = NormalizeKey(scrobble.Artist);
        var trackKey = NormalizeKey(scrobble.Track);

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITrackInfoRepository>();

        var cached = await repository.GetAsync(artistKey, trackKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Track metadata cache hit for {Artist} - {Track}", scrobble.Artist, scrobble.Track);
            return cached.Found
                ? new TrackMetadata(cached.CanonicalArtist, cached.CanonicalTrack, cached.CanonicalAlbum)
                : null;
        }

        var metadata = await FetchTrackMetadataFromLastfmAsync(scrobble.Artist, scrobble.Track, cancellationToken);

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await repository.AddAsync(new TrackInfo
        {
            ArtistKey = artistKey,
            TrackKey = trackKey,
            Found = metadata is not null,
            CanonicalArtist = metadata?.Artist,
            CanonicalTrack = metadata?.Track,
            CanonicalAlbum = metadata?.Album,
            FetchedAt = DateTime.UtcNow
        }, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent insert already cached this (artist, track); the unique index rejected ours.
            // The metadata we just fetched is still valid to use, so swallow and continue.
            _logger.LogDebug("Track metadata cache entry already present for {Artist} - {Track}", scrobble.Artist, scrobble.Track);
        }

        return metadata;
    }

    private static string NormalizeKey(string value) => value.Trim().ToLowerInvariant();

    private async Task<TrackMetadata?> FetchTrackMetadataFromLastfmAsync(string artist, string track, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["method"] = "track.getInfo",
            ["api_key"] = _options.ApiKey!,
            ["artist"] = artist,
            ["track"] = track,
            ["format"] = "json"
        };

        var query = string.Join("&", parameters
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        var url = $"{_options.ApiRoot}?{query}";

        var client = _httpClientFactory.CreateClient("RelayHttpClient");
        using var response = await client.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("error", out _))
            return null;

        if (!doc.RootElement.TryGetProperty("track", out var trackEl))
            return null;

        var canonicalTrack = trackEl.TryGetProperty("name", out var trackName) ? trackName.GetString() : null;

        string? canonicalArtist = null;
        if (trackEl.TryGetProperty("artist", out var artistEl))
        {
            if (artistEl.ValueKind == JsonValueKind.String)
                canonicalArtist = artistEl.GetString();
            else if (artistEl.ValueKind == JsonValueKind.Object && artistEl.TryGetProperty("name", out var artistName))
                canonicalArtist = artistName.GetString();
        }

        string? album = null;
        if (trackEl.TryGetProperty("album", out var albumEl))
        {
            if (albumEl.ValueKind == JsonValueKind.String)
                album = albumEl.GetString();
            else if (albumEl.ValueKind == JsonValueKind.Object && albumEl.TryGetProperty("title", out var title))
                album = title.GetString();
        }

        if (string.IsNullOrWhiteSpace(canonicalTrack))
            return null;

        return new TrackMetadata(canonicalArtist, canonicalTrack, album);
    }

    private sealed record TrackMetadata(string? Artist, string? Track, string? Album);
}
