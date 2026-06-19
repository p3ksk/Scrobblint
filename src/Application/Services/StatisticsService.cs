using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Common;
using Scrobblint.Domain.Enums;
using Scrobblint.Shared.Stats;

namespace Scrobblint.Application.Services;

public sealed class StatisticsService : IStatisticsService
{
    private readonly IScrobbleRepository _scrobbles;
    private readonly IUserRepository _users;
    private readonly IUserSettingsRepository _settings;

    public StatisticsService(
        IScrobbleRepository scrobbles,
        IUserRepository users,
        IUserSettingsRepository settings)
    {
        _scrobbles = scrobbles;
        _users = users;
        _settings = settings;
    }

    public async Task<Result<StatsResponse>> GetStatsAsync(
        string username, ViewerContext viewer,
        DateTime? from = null, DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByUsernameAsync(username, cancellationToken);
        if (user is null || user.IsDisabled)
            return Result<StatsResponse>.NotFound("User not found.");

        var settings = await _settings.GetByUserIdAsync(user.Id, cancellationToken);
        var visibility = settings?.ProfileVisibility ?? ProfileVisibility.Public;
        if (visibility == ProfileVisibility.Private && !viewer.CanSeePrivate(user.Id))
            return Result<StatsResponse>.Forbidden("This profile is private.");

        // Each call is a separate, individually-optimised aggregate query.
        var total = await _scrobbles.CountAsync(user.Id, from, to, cancellationToken);
        var uniqueArtists = await _scrobbles.CountDistinctArtistsAsync(user.Id, from, to, cancellationToken);
        var uniqueTracks = await _scrobbles.CountDistinctTracksAsync(user.Id, from, to, cancellationToken);
        var uniqueAlbums = await _scrobbles.CountDistinctAlbumsAsync(user.Id, from, to, cancellationToken);
        var topArtists = await _scrobbles.GetTopArtistsAsync(user.Id, AppConstants.TopListSize, from, to, cancellationToken);
        var topAlbums = await _scrobbles.GetTopAlbumsAsync(user.Id, AppConstants.TopListSize, from, to, cancellationToken);
        var topTracks = await _scrobbles.GetTopTracksAsync(user.Id, AppConstants.TopListSize, from, to, cancellationToken);
        var monthly = await _scrobbles.GetMonthlyChartAsync(user.Id, from, to, cancellationToken);
        var daily = await _scrobbles.GetDailyChartAsync(user.Id, from, to, cancellationToken);
        var hourly = await _scrobbles.GetHourlyChartAsync(user.Id, from, to, cancellationToken);
        var dayOfWeek = await _scrobbles.GetDayOfWeekChartAsync(user.Id, from, to, cancellationToken);
        var yearly = await _scrobbles.GetYearlyChartAsync(user.Id, from, to, cancellationToken);

        return Result<StatsResponse>.Ok(new StatsResponse(
            total, uniqueArtists, uniqueTracks, uniqueAlbums,
            topArtists, topAlbums, topTracks, monthly, daily, hourly, dayOfWeek, yearly));
    }
}
