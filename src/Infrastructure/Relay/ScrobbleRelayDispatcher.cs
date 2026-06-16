using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Abstractions.Relay;

namespace Scrobblint.Infrastructure.Relay;

/// <summary>
/// Background worker that drains the relay queue and forwards each user's listens to their enabled
/// external services. Failures are logged and swallowed — relaying must never affect local scrobbling.
/// </summary>
public sealed class ScrobbleRelayDispatcher : BackgroundService
{
    private readonly IScrobbleRelayQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyDictionary<Domain.Enums.ScrobbleProvider, IScrobbleRelay> _relays;
    private readonly ILogger<ScrobbleRelayDispatcher> _logger;

    public ScrobbleRelayDispatcher(
        IScrobbleRelayQueue queue,
        IServiceScopeFactory scopeFactory,
        IEnumerable<IScrobbleRelay> relays,
        ILogger<ScrobbleRelayDispatcher> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _relays = relays.ToDictionary(r => r.Provider);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error relaying scrobbles for user {UserId}", job.UserId);
            }
        }
    }

    private async Task ProcessAsync(ScrobbleRelayJob job, CancellationToken cancellationToken)
    {
        if (job.Tracks.Count == 0) return;

        using var scope = _scopeFactory.CreateScope();
        var connections = scope.ServiceProvider.GetRequiredService<IExternalConnectionRepository>();

        var enabled = await connections.GetEnabledByUserAsync(job.UserId, cancellationToken);
        if (enabled.Count == 0) return;

        foreach (var connection in enabled)
        {
            if (!_relays.TryGetValue(connection.Provider, out var relay) || !relay.IsConfigured)
                continue;

            try
            {
                var result = await relay.SendAsync(connection, job.Tracks, cancellationToken);
                if (result.Success)
                    _logger.LogInformation("Relayed {Count} listen(s) to {Provider} for user {UserId}",
                        result.Accepted, connection.Provider, job.UserId);
                else
                    _logger.LogWarning("Failed to relay to {Provider} for user {UserId}: {Error}",
                        connection.Provider, job.UserId, result.Error);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error relaying to {Provider} for user {UserId}", connection.Provider, job.UserId);
            }
        }
    }
}
