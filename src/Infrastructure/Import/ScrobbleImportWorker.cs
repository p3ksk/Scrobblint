using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Abstractions.Relay;
using Scrobblint.Application.Services;
using Scrobblint.Infrastructure.Configuration;

namespace Scrobblint.Infrastructure.Import;

/// <summary>
/// Drives history imports in the background: processes one page per scoped call (so the DbContext
/// never grows over a multi-hour job), pacing requests politely. Resumes any imports left running
/// after a restart.
/// </summary>
public sealed class ScrobbleImportWorker : BackgroundService
{
    private readonly IScrobbleImportQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ImportOptions _options;
    private readonly ILogger<ScrobbleImportWorker> _logger;

    public ScrobbleImportWorker(
        IScrobbleImportQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<ImportOptions> options,
        ILogger<ScrobbleImportWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ResumeInterruptedAsync(stoppingToken);

        await foreach (var importId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await RunImportAsync(importId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing import {ImportId}", importId);
            }
        }
    }

    private async Task RunImportAsync(Guid importId, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(Math.Max(0, _options.PageDelayMilliseconds));

        while (!cancellationToken.IsCancellationRequested)
        {
            bool more;
            using (var scope = _scopeFactory.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IScrobbleImportService>();
                more = await service.ProcessNextChunkAsync(importId, cancellationToken);
            }

            if (!more) break;
            await Task.Delay(delay, cancellationToken);
        }
    }

    private async Task ResumeInterruptedAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var imports = scope.ServiceProvider.GetRequiredService<IScrobbleImportRepository>();
            var resumable = await imports.GetResumableAsync(cancellationToken);
            foreach (var import in resumable)
            {
                _logger.LogInformation("Resuming interrupted import {ImportId} (page {Page})", import.Id, import.NextPage);
                _queue.Enqueue(import.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scan for resumable imports on start-up");
        }
    }
}
