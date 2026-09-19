using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrobblint.Application.Abstractions;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Common;
using Scrobblint.Application.Services;
using Scrobblint.Infrastructure.Configuration;

namespace Scrobblint.Infrastructure.Statistics;

/// <summary>
/// Periodically recomputes and persists the statistics snapshots that the read path serves, so
/// browsing does not pay for the aggregate queries. Refreshes the site-wide snapshot and each
/// recently-active user's all-time snapshot; inactive users are computed on demand on first request.
/// </summary>
public sealed class StatisticsPrecomputeWorker : BackgroundService
{
    /// <summary>Lets the rest of the app (migrations, seeding) settle before the first run.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StatisticsOptions _options;
    private readonly ILogger<StatisticsPrecomputeWorker> _logger;

    public StatisticsPrecomputeWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<StatisticsOptions> options,
        ILogger<StatisticsPrecomputeWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes));
        _logger.LogInformation("Statistics precompute worker started (interval {Interval})", interval);

        if (_options.RunOnStartup)
        {
            try
            {
                await Task.Delay(StartupDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            await SafeRunAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await SafeRunAsync(stoppingToken);
        }
    }

    /// <summary>Runs one precompute pass; unhandled failures are logged and swallowed so the loop survives.</summary>
    public async Task SafeRunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error during the statistics precompute run");
        }
    }

    /// <summary>Recomputes the global snapshot and every active user's all-time snapshot.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        DateTime now;
        IReadOnlyList<Guid> activeUserIds;

        // Global snapshot and the active-user lookup share one scope; each user is recomputed in its
        // own scope so the scoped change tracker never accumulates across the whole run.
        using (var scope = _scopeFactory.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var computer = sp.GetRequiredService<IStatisticsComputer>();
            var snapshots = sp.GetRequiredService<IStatisticsRepository>();
            var scrobbles = sp.GetRequiredService<IScrobbleRepository>();
            var clock = sp.GetRequiredService<IClock>();

            now = clock.UtcNow;

            var global = await computer.ComputeGlobalStatsAsync(cancellationToken);
            await snapshots.UpsertGlobalAsync(
                StatisticsSerializer.SerializeGlobalStats(global), now, cancellationToken);
            _logger.LogInformation("Precomputed global statistics ({Scrobbles} scrobbles)", global.TotalScrobbles);

            var since = now.AddDays(-Math.Max(1, _options.ActiveWindowDays));
            activeUserIds = await scrobbles.GetActiveUserIdsAsync(since, cancellationToken);
        }

        _logger.LogInformation("Precomputing statistics for {Count} active user(s)", activeUserIds.Count);

        var computed = 0;
        foreach (var userId in activeUserIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sp = scope.ServiceProvider;
                var computer = sp.GetRequiredService<IStatisticsComputer>();
                var snapshots = sp.GetRequiredService<IStatisticsRepository>();
                var clock = sp.GetRequiredService<IClock>();

                var stats = await computer.ComputeUserStatsAsync(userId, cancellationToken);
                if (stats is null)
                    continue; // user vanished or was disabled mid-run

                await snapshots.UpsertUserAsync(
                    userId, StatisticsSerializer.SerializeUserStats(stats), clock.UtcNow, cancellationToken);
                computed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to precompute statistics for user {UserId}", userId);
            }
        }

        _logger.LogInformation(
            "Statistics precompute run finished: {Computed}/{Total} user snapshot(s) updated",
            computed, activeUserIds.Count);
    }
}
