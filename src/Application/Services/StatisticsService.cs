using Scrobblint.Application.Abstractions;
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
    private readonly IClock _clock;

    public StatisticsService(
        IScrobbleRepository scrobbles,
        IUserRepository users,
        IUserSettingsRepository settings,
        IClock clock)
    {
        _scrobbles = scrobbles;
        _users = users;
        _settings = settings;
        _clock = clock;
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

        // Time-based charts follow the user's own timezone. The daily chart defaults to the trailing
        // window of local days when no range is given.
        var zone = TimeZones.FindOrUtc(settings?.Timezone);
        var dailyFrom = from ?? TimeZones.StartOfDayUtc(
            TimeZones.Today(zone, _clock.UtcNow).AddDays(-(AppConstants.DailyChartDays - 1)), zone);
        var timestamps = await _scrobbles.GetTimestampsAsync(user.Id, from, to, cancellationToken);
        var charts = ListeningCharts.Build(timestamps, zone, dailyFrom);

        return Result<StatsResponse>.Ok(new StatsResponse(
            total, uniqueArtists, uniqueTracks, uniqueAlbums,
            topArtists, topAlbums, topTracks,
            charts.Monthly, charts.Daily, charts.Hourly, charts.DayOfWeek, charts.Yearly,
            new DayHourHeatmap(charts.DayHourHeatmap)));
    }

    public async Task<Result<GlobalStatsResponse>> GetGlobalStatsAsync(CancellationToken cancellationToken = default)
    {
        var total = await _scrobbles.CountAllAsync(cancellationToken);
        var totalUsers = await _users.CountAllAsync(cancellationToken);
        var uniqueArtists = await _scrobbles.CountDistinctArtistsGlobalAsync(cancellationToken);
        var uniqueTracks = await _scrobbles.CountDistinctTracksGlobalAsync(cancellationToken);
        var topArtists = await _scrobbles.GetTopArtistsGlobalAsync(AppConstants.TopListSize, cancellationToken);
        var topAlbums = await _scrobbles.GetTopAlbumsGlobalAsync(AppConstants.TopListSize, cancellationToken);
        var topTracks = await _scrobbles.GetTopTracksGlobalAsync(AppConstants.TopListSize, cancellationToken);

        return Result<GlobalStatsResponse>.Ok(new GlobalStatsResponse(
            total, totalUsers, uniqueArtists, uniqueTracks,
            topArtists, topAlbums, topTracks));
    }
}
