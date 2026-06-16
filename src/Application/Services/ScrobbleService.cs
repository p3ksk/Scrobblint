using Microsoft.Extensions.Logging;
using Scrobblint.Application.Abstractions;
using Scrobblint.Application.Abstractions.Persistence;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<ScrobbleService> _logger;

    public ScrobbleService(
        IScrobbleRepository scrobbles,
        IUserRepository users,
        IUserSettingsRepository settings,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<ScrobbleService> logger)
    {
        _scrobbles = scrobbles;
        _users = users;
        _settings = settings;
        _unitOfWork = unitOfWork;
        _clock = clock;
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

        _logger.LogInformation("Accepted {Count} scrobble(s) for user {UserId}", entities.Count, userId);
        return Result<ScrobbleSubmitResponse>.Ok(new ScrobbleSubmitResponse(entities.Count));
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
