using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Abstractions.Relay;
using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;

namespace Scrobblint.Infrastructure.Relay;

public sealed class FailedRelayWorker : BackgroundService
{
    private const int MaxRetryCount = 8;
    private const int BatchSize = 10;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyDictionary<ScrobbleProvider, IScrobbleRelay> _relays;
    private readonly IFailedRelayWorkerTrigger _trigger;
    private readonly ILogger<FailedRelayWorker> _logger;

    public FailedRelayWorker(
        IServiceScopeFactory scopeFactory,
        IEnumerable<IScrobbleRelay> relays,
        IFailedRelayWorkerTrigger trigger,
        ILogger<FailedRelayWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _relays = relays.ToDictionary(r => r.Provider);
        _trigger = trigger;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Failed relay retry worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Wait first (rather than polling immediately on startup): gives the rest of the app
            // a moment to settle, and lets an admin's "run now" action skip the wait early.
            try
            {
                await _trigger.WaitForSignalAsync(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in failed relay retry worker");
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<FailedRelay> pending;
        using (var scope = _scopeFactory.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IFailedRelayRepository>();
            pending = await repo.GetPendingAtAsync(DateTime.UtcNow, BatchSize, cancellationToken);
        }

        if (pending.Count == 0) return;

        foreach (var item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await RetryAsync(item, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrying failed relay {Id}", item.Id);
            }
        }
    }

    private async Task RetryAsync(FailedRelay item, CancellationToken cancellationToken)
    {
        if (!_relays.TryGetValue(item.Provider, out var relay) || !relay.IsConfigured)
            return;

        RelayTrack[] tracks;
        try
        {
            tracks = JsonSerializer.Deserialize<RelayTrack[]>(item.TracksJson) ?? Array.Empty<RelayTrack>();
        }
        catch (JsonException)
        {
            tracks = Array.Empty<RelayTrack>();
        }

        if (tracks.Length == 0)
        {
            await FinalizeAsync(item, RelayStatus.Failed, "Invalid track payload", cancellationToken);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var connections = scope.ServiceProvider.GetRequiredService<IExternalConnectionRepository>();
        var repo = scope.ServiceProvider.GetRequiredService<IFailedRelayRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var connection = await connections.GetAsync(item.UserId, item.Provider, cancellationToken);
        if (connection is null || !connection.IsEnabled)
        {
            repo.Update(item);
            item.Status = RelayStatus.Cancelled;
            item.LastError = "Connection no longer exists or is disabled";
            item.UpdatedAt = DateTime.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            var result = await relay.SendAsync(connection, tracks, cancellationToken);
            if (result.Success)
            {
                repo.Update(item);
                item.Status = RelayStatus.Completed;
                item.LastError = null;
                item.UpdatedAt = DateTime.UtcNow;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Retry succeeded for failed relay {Id} ({Provider}, user {UserId})",
                    item.Id, item.Provider, item.UserId);
            }
            else
            {
                await BumpRetryAsync(repo, unitOfWork, item, result.Error, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await BumpRetryAsync(repo, unitOfWork, item, ex.Message, cancellationToken);
        }
    }

    private async Task BumpRetryAsync(
        IFailedRelayRepository repo, IUnitOfWork unitOfWork,
        FailedRelay item, string? error, CancellationToken cancellationToken)
    {
        item.RetryCount++;
        item.LastError = error;
        item.UpdatedAt = DateTime.UtcNow;

        if (item.RetryCount >= MaxRetryCount)
        {
            item.Status = RelayStatus.Failed;
            _logger.LogWarning("Failed relay {Id} ({Provider}, user {UserId}) permanently failed after {RetryCount} attempts",
                item.Id, item.Provider, item.UserId, item.RetryCount);
        }
        else
        {
            var seconds = Math.Min(Math.Pow(2, item.RetryCount), MaxBackoff.TotalSeconds);
            item.NextRetryAt = DateTime.UtcNow.AddSeconds(seconds);
            _logger.LogDebug("Failed relay {Id} will retry in {Seconds}s (attempt {RetryCount})",
                item.Id, seconds, item.RetryCount);
        }

        repo.Update(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task FinalizeAsync(
        FailedRelay item, RelayStatus status, string? error, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IFailedRelayRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        repo.Update(item);
        item.Status = status;
        item.LastError = error;
        item.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
