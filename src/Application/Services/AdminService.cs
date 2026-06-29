using System.Diagnostics;
using Scrobblint.Application.Abstractions.CoverArt;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Abstractions.Pipeline;
using Scrobblint.Application.Abstractions.Relay;

namespace Scrobblint.Application.Services;

/// <summary>Summary of the application runtime for the admin status page.</summary>
public sealed record AdminStatus(
    DateTime StartedAt,
    long UptimeSeconds,
    long WorkingSetMb,
    long GcHeapMb,
    int ThreadPoolThreads,
    int TotalUsers,
    int TotalScrobbles,
    int EnrichQueueDepth,
    int SaveQueueDepth,
    int RelayQueueDepth,
    int ImportQueueDepth,
    int TrackInfoCacheEntries,
    int CoverArtEntries,
    long CoverArtCacheHits,
    long CoverArtCacheMisses);

/// <summary>
/// Service that collects process, application, and cache metrics for the admin dashboard.
/// </summary>
public interface IAdminService
{
    Task<AdminStatus> GetStatusAsync(CancellationToken ct = default);
}

public sealed class AdminService : IAdminService
{
    private static readonly DateTime StartedAt = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    private readonly IScrobbleRepository _scrobbles;
    private readonly IUserRepository _users;
    private readonly ITrackInfoRepository _trackInfo;
    private readonly IScrobblePipelineQueue _enrichQueue;
    private readonly ISaveQueue _saveQueue;
    private readonly IScrobbleRelayQueue _relayQueue;
    private readonly IScrobbleImportQueue _importQueue;
    private readonly ICoverArtProvider _coverArt;

    public AdminService(
        IScrobbleRepository scrobbles,
        IUserRepository users,
        ITrackInfoRepository trackInfo,
        IScrobblePipelineQueue enrichQueue,
        ISaveQueue saveQueue,
        IScrobbleRelayQueue relayQueue,
        IScrobbleImportQueue importQueue,
        ICoverArtProvider coverArt)
    {
        _scrobbles = scrobbles;
        _users = users;
        _trackInfo = trackInfo;
        _enrichQueue = enrichQueue;
        _saveQueue = saveQueue;
        _relayQueue = relayQueue;
        _importQueue = importQueue;
        _coverArt = coverArt;
    }

    public async Task<AdminStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var proc = Process.GetCurrentProcess();
        proc.Refresh();

        var totalScrobbles = await _scrobbles.CountAllAsync(ct);
        var totalUsers = await _users.CountAllAsync(ct);
        var trackInfoEntries = await _trackInfo.CountAsync(ct);

        return new AdminStatus(
            StartedAt: StartedAt,
            UptimeSeconds: (long)(DateTime.UtcNow - StartedAt).TotalSeconds,
            WorkingSetMb: proc.WorkingSet64 / (1024 * 1024),
            GcHeapMb: GC.GetTotalMemory(false) / (1024 * 1024),
            ThreadPoolThreads: ThreadPool.ThreadCount,
            TotalUsers: totalUsers,
            TotalScrobbles: totalScrobbles,
            EnrichQueueDepth: _enrichQueue.Count,
            SaveQueueDepth: _saveQueue.Count,
            RelayQueueDepth: _relayQueue.Count,
            ImportQueueDepth: _importQueue.Count,
            TrackInfoCacheEntries: trackInfoEntries,
            CoverArtEntries: _coverArt.CacheEntryCount,
            CoverArtCacheHits: _coverArt.CacheHits,
            CoverArtCacheMisses: _coverArt.CacheMisses
        );
    }
}