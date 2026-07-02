using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrobblint.Application.Abstractions.Pipeline;
using Scrobblint.Application.Abstractions.Relay;

namespace Scrobblint.Infrastructure.Pipeline;

/// <summary>
/// During shutdown, completes pipeline queues before workers are cancelled so each
/// <see cref="BackgroundService"/> naturally drains buffered items through
/// <see cref="System.Threading.Channels.ChannelReader{T}.ReadAllAsync"/>.
/// Registered on <see cref="IHostApplicationLifetime.ApplicationStopping"/>
/// which fires before any hosted service is stopped.
/// </summary>
public sealed class PipelineDrainHostedService : IHostedService
{
    private readonly IScrobblePipelineQueue _pipelineQueue;
    private readonly ISaveQueue _saveQueue;
    private readonly IScrobbleRelayQueue _relayQueue;
    private readonly ILogger<PipelineDrainHostedService> _logger;

    public PipelineDrainHostedService(
        IScrobblePipelineQueue pipelineQueue,
        ISaveQueue saveQueue,
        IScrobbleRelayQueue relayQueue,
        IHostApplicationLifetime lifetime,
        ILogger<PipelineDrainHostedService> logger)
    {
        _pipelineQueue = pipelineQueue;
        _saveQueue = saveQueue;
        _relayQueue = relayQueue;
        _logger = logger;

        lifetime.ApplicationStopping.Register(() =>
        {
            _logger.LogInformation("Pipeline shutdown: completing queues for graceful drain");

            _pipelineQueue.Complete();
            _logger.LogInformation("Pipeline shutdown: Stage 1 (Pipeline) completed");

            _saveQueue.Complete();
            _logger.LogInformation("Pipeline shutdown: Stage 2 (Save) completed");

            _relayQueue.Complete();
            _logger.LogInformation("Pipeline shutdown: Stage 3 (Relay) completed");
        });
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
