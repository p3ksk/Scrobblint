using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Scrobblint.Application.Abstractions;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Abstractions.Relay;
using Scrobblint.Application.Common;
using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;
using Scrobblint.Shared.Common;
using Scrobblint.Shared.Scrobbles;

namespace Scrobblint.Application.Services;

public sealed class ScrobbleService : IScrobbleService
{
    private readonly IScrobbleRepository _scrobbles;
    private readonly IUserRepository _users;
    private readonly IUserSettingsRepository _settings;
    private readonly IExternalConnectionRepository _connections;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IScrobbleRelayQueue _relayQueue;
    private readonly IEnumerable<IScrobbleRelay> _relays;
    private readonly IClock _clock;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ScrobbleService> _logger;

    public ScrobbleService(
        IScrobbleRepository scrobbles,
        IUserRepository users,
        IUserSettingsRepository settings,
        IExternalConnectionRepository connections,
        IUnitOfWork unitOfWork,
        IScrobbleRelayQueue relayQueue,
        IEnumerable<IScrobbleRelay> relays,
        IClock clock,
        IMemoryCache cache,
        ILogger<ScrobbleService> logger)
    {
        _scrobbles = scrobbles;
        _users = users;
        _settings = settings;
        _connections = connections;
        _unitOfWork = unitOfWork;
        _relayQueue = relayQueue;
        _relays = relays;
        _clock = clock;
        _cache = cache;
        _logger = logger;
    }

    public Task<Result<ScrobbleSubmitResponse>> SubmitAsync(Guid userId, ScrobbleRequest request, CancellationToken cancellationToken = default)
        => SubmitBatchAsync(userId, new ScrobbleBatchRequest(new[] { request }), cancellationToken);

    public async Task<Result<ScrobbleSubmitResponse>> SubmitBatchAsync(Guid userId, ScrobbleBatchRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Scrobbles is null || request.Scrobbles.Count == 0)
            return Result<ScrobbleSubmitResponse>.Invalid(
                new Dictionary<string, string[]> { ["scrobbles"] = new[] { "At least one scrobble is required." } });

        if (request.Scrobbles.Count > AppConstants.MaxBatchSize)
            return Result<ScrobbleSubmitResponse>.Invalid(
                new Dictionary<string, string[]> { ["scrobbles"] = new[] { $"A batch may contain at most {AppConstants.MaxBatchSize} scrobbles." } });

        var now = _clock.UtcNow;
        var validation = new ValidationBuilder();
        var entities = new List<Scrobble>(request.Scrobbles.Count);

        for (var i = 0; i < request.Scrobbles.Count; i++)
        {
            var item = request.Scrobbles[i];
            var artist = item.Artist?.Trim() ?? string.Empty;
            var track = item.Track?.Trim() ?? string.Empty;
            var album = string.IsNullOrWhiteSpace(item.Album) ? null : item.Album!.Trim();

            if (string.IsNullOrEmpty(artist)) validation.Add($"scrobbles[{i}].artist", "Artist is required.");
            if (string.IsNullOrEmpty(track)) validation.Add($"scrobbles[{i}].track", "Track is required.");
            if (artist.Length > AppConstants.FieldMaxLength) validation.Add($"scrobbles[{i}].artist", "Artist is too long.");
            if (track.Length > AppConstants.FieldMaxLength) validation.Add($"scrobbles[{i}].track", "Track is too long.");
            if (album is { Length: > AppConstants.FieldMaxLength }) validation.Add($"scrobbles[{i}].album", "Album is too long.");

            var timestamp = ResolveTimestamp(item.Timestamp, now);
            if (timestamp > now.AddDays(1))
                validation.Add($"scrobbles[{i}].timestamp", "Timestamp cannot be in the future.");

            entities.Add(new Scrobble
            {
                UserId = userId,
                Artist = artist,
                Track = track,
                Album = album,
                Timestamp = timestamp,
                CreatedAt = now
            });
        }

        if (validation.HasErrors)
            return Result<ScrobbleSubmitResponse>.Invalid(validation.Build());

        await _scrobbles.AddRangeAsync(entities, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Forward to any external services the user has linked. This is best-effort and runs on a
        // background dispatcher, so it never blocks or fails the local scrobble.
        var relayTracks = entities
            .Select(e => new RelayTrack(e.Artist, e.Track, e.Album, Mappers.ToUnix(e.Timestamp)))
            .ToList();
        _relayQueue.Enqueue(new ScrobbleRelayJob(userId, relayTracks));

        _logger.LogInformation("Accepted {Count} scrobble(s) for user {UserId}", entities.Count, userId);
        return Result<ScrobbleSubmitResponse>.Ok(new ScrobbleSubmitResponse(entities.Count));
    }

    public async Task<Result> UpdateNowPlayingAsync(Guid userId, NowPlayingRequest request, CancellationToken cancellationToken = default)
    {
        var artist = request.Artist?.Trim() ?? string.Empty;
        var track = request.Track?.Trim() ?? string.Empty;
        var album = string.IsNullOrWhiteSpace(request.Album) ? null : request.Album!.Trim();

        var validation = new ValidationBuilder();
        if (string.IsNullOrEmpty(artist)) validation.Add("artist", "Artist is required.");
        if (string.IsNullOrEmpty(track)) validation.Add("track", "Track is required.");
        if (artist.Length > AppConstants.FieldMaxLength) validation.Add("artist", "Artist is too long.");
        if (track.Length > AppConstants.FieldMaxLength) validation.Add("track", "Track is too long.");
        if (album is { Length: > AppConstants.FieldMaxLength }) validation.Add("album", "Album is too long.");
        if (validation.HasErrors)
            return Result.Invalid(validation.Build());

        var enabled = await _connections.GetEnabledByUserAsync(userId, cancellationToken);
        var relayLookup = _relays.ToDictionary(r => r.Provider);

        foreach (var connection in enabled)
        {
            if (!relayLookup.TryGetValue(connection.Provider, out var relay) || !relay.IsConfigured)
                continue;

            try
            {
                var relayResult = await relay.SendNowPlayingAsync(connection, artist, track, album, cancellationToken);
                if (relayResult.Success)
                    _logger.LogInformation("Now-playing relayed to {Provider} for user {UserId}", connection.Provider, userId);
                else
                    _logger.LogWarning("Now-playing relay to {Provider} failed for user {UserId}: {Error}", connection.Provider, userId, relayResult.Error);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Now-playing relay to {Provider} errored for user {UserId}", connection.Provider, userId);
            }
        }

        _cache.Set($"np:{userId}", new NowPlayingResponse(artist, track, album, _clock.UtcNow),
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2) });

        return Result.Ok();
    }

    public NowPlayingResponse? GetNowPlaying(Guid userId) =>
        _cache.Get<NowPlayingResponse>($"np:{userId}");

    public async Task<NowPlayingResponse?> GetNowPlayingByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByUsernameAsync(username, cancellationToken);
        return user is null ? null : _cache.Get<NowPlayingResponse>($"np:{user.Id}");
    }

    public async Task<Result<PagedResponse<ScrobbleResponse>>> GetRecentAsync(
        string username, int page, int pageSize, ViewerContext viewer, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByUsernameAsync(username, cancellationToken);
        if (user is null || user.IsDisabled)
            return Result<PagedResponse<ScrobbleResponse>>.NotFound("User not found.");

        var visibility = await ResolveVisibilityAsync(user.Id, cancellationToken);
        if (visibility == ProfileVisibility.Private && !viewer.CanSeePrivate(user.Id))
            return Result<PagedResponse<ScrobbleResponse>>.Forbidden("This profile is private.");

        page = AppConstants.ClampPage(page);
        pageSize = AppConstants.ClampPageSize(pageSize);

        var (items, total) = await _scrobbles.GetRecentAsync(user.Id, page, pageSize, cancellationToken);
        var mapped = items.Select(s => s.ToResponse()).ToList();
        return Result<PagedResponse<ScrobbleResponse>>.Ok(
            new PagedResponse<ScrobbleResponse>(mapped, page, pageSize, total));
    }

    private static DateTime ResolveTimestamp(long? unixSeconds, DateTime now)
    {
        if (unixSeconds is null or <= 0) return now;
        try
        {
            return Mappers.FromUnix(unixSeconds.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return now;
        }
    }

    private async Task<ProfileVisibility> ResolveVisibilityAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = await _settings.GetByUserIdAsync(userId, cancellationToken);
        return settings?.ProfileVisibility ?? ProfileVisibility.Public;
    }
}
