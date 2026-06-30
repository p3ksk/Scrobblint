using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Abstractions.Pipeline;
using Scrobblint.Application.Abstractions.Relay;
using Scrobblint.Application.Common;
using Scrobblint.Domain.Entities;

namespace Scrobblint.Infrastructure.Pipeline;

/// <summary>
/// Stage 2: Save pipeline worker.
/// Consumes enriched scrobbles, saves to database, forwards to relay queue.
/// </summary>
public sealed class SaveStageWorker : BackgroundService
{
    private readonly ISaveQueue _inputQueue;
    private readonly IScrobbleRelayQueue _outputQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SaveStageWorker> _logger;

    public SaveStageWorker(
        ISaveQueue inputQueue,
        IScrobbleRelayQueue outputQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<SaveStageWorker> logger)
    {
        _inputQueue = inputQueue;
        _outputQueue = outputQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Pipeline Stage 2 (Save) started");

        await foreach (var scrobble in _inputQueue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                // Save enriched scrobble to database
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<IScrobbleRepository>();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    var entity = new Scrobble
                    {
                        UserId = scrobble.UserId,
                        Artist = scrobble.Artist,
                        Track = scrobble.Track,
                        Album = scrobble.Album,
                        Timestamp = scrobble.Timestamp,
                        CreatedAt = scrobble.CreatedAt
                    };

                    await repository.AddAsync(entity, stoppingToken);
                    await unitOfWork.SaveChangesAsync(stoppingToken);

                    _logger.LogDebug("Saved scrobble to DB: {Artist} - {Album} - {Track}", scrobble.Artist, scrobble.Album ?? "(empty)", scrobble.Track);
                }

                // Forward to relay queue (Stage 3)
                var relayTrack = new RelayTrack(
                    scrobble.Artist,
                    scrobble.Track,
                    scrobble.Album,
                    Mappers.ToUnix(scrobble.Timestamp));

                _outputQueue.Enqueue(new ScrobbleRelayJob(scrobble.UserId, new[] { relayTrack }));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in save stage for {Artist} - {Track}", scrobble.Artist, scrobble.Track);
                // Don't forward to relay on save failure
            }
        }

        _logger.LogInformation("Pipeline Stage 2 (Save) stopped");
    }
}
