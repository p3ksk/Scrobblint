using System.Diagnostics;
using Scrobblint.Application.Abstractions.CoverArt;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Abstractions.Pipeline;
using Scrobblint.Application.Abstractions.Relay;
using Scrobblint.Application.Common;
using Scrobblint.Domain.Enums;
using Scrobblint.Shared.Common;
using Scrobblint.Shared.Relay;

namespace Scrobblint.Application.Services;

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
    long CoverArtCacheMisses,
    int PendingFailedRelays,
    int PermanentlyFailedRelays);

public interface IAdminService
{
    Task<AdminStatus> GetStatusAsync(CancellationToken ct = default);

    Task<Result<PagedResponse<AdminFailedRelayListItem>>> GetFailedRelaysAsync(
        int page, int pageSize, CancellationToken ct = default);

    /// <summary>Resets a retry-cache record so the relay worker picks it up on its next poll.</summary>
    Task<Result> RetryFailedRelayAsync(Guid id, CancellationToken ct = default);

    Task<Result> DeleteFailedRelayAsync(Guid id, CancellationToken ct = default);

    Task<Result<AdminFailedRelayDetail>> GetFailedRelayDetailAsync(Guid id, CancellationToken ct = default);

    /// <summary>Resets every permanently-failed record back to pending. Returns the number reset.</summary>
    Task<Result<int>> RetryAllFailedAsync(CancellationToken ct = default);
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
    private readonly IFailedRelayRepository _failedRelays;
    private readonly IUnitOfWork _unitOfWork;

    public AdminService(
        IScrobbleRepository scrobbles,
        IUserRepository users,
        ITrackInfoRepository trackInfo,
        IScrobblePipelineQueue enrichQueue,
        ISaveQueue saveQueue,
        IScrobbleRelayQueue relayQueue,
        IScrobbleImportQueue importQueue,
        ICoverArtProvider coverArt,
        IFailedRelayRepository failedRelays,
        IUnitOfWork unitOfWork)
    {
        _scrobbles = scrobbles;
        _users = users;
        _trackInfo = trackInfo;
        _enrichQueue = enrichQueue;
        _saveQueue = saveQueue;
        _relayQueue = relayQueue;
        _importQueue = importQueue;
        _coverArt = coverArt;
        _failedRelays = failedRelays;
        _unitOfWork = unitOfWork;
    }

    public async Task<AdminStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var proc = Process.GetCurrentProcess();
        proc.Refresh();

        var totalScrobbles = await _scrobbles.CountAllAsync(ct);
        var totalUsers = await _users.CountAllAsync(ct);
        var trackInfoEntries = await _trackInfo.CountAsync(ct);
        var pendingFailed = await _failedRelays.CountByStatusAsync(RelayStatus.Pending, ct);
        var permanentlyFailed = await _failedRelays.CountByStatusAsync(RelayStatus.Failed, ct);

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
            CoverArtCacheMisses: _coverArt.CacheMisses,
            PendingFailedRelays: pendingFailed,
            PermanentlyFailedRelays: permanentlyFailed
        );
    }

    public async Task<Result<PagedResponse<AdminFailedRelayListItem>>> GetFailedRelaysAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        page = AppConstants.ClampPage(page);
        pageSize = AppConstants.ClampPageSize(pageSize);
        var (items, total) = await _failedRelays.GetAdminListAsync(page, pageSize, ct);
        return Result<PagedResponse<AdminFailedRelayListItem>>.Ok(
            new PagedResponse<AdminFailedRelayListItem>(items, page, pageSize, total));
    }

    public async Task<Result> RetryFailedRelayAsync(Guid id, CancellationToken ct = default)
    {
        var failedRelay = await _failedRelays.GetByIdAsync(id, ct);
        if (failedRelay is null)
            return Result.NotFound("Retry cache record not found.");

        failedRelay.Status = RelayStatus.Pending;
        failedRelay.RetryCount = 0;
        failedRelay.NextRetryAt = DateTime.UtcNow;
        failedRelay.UpdatedAt = DateTime.UtcNow;
        _failedRelays.Update(failedRelay);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Ok();
    }

    public async Task<Result> DeleteFailedRelayAsync(Guid id, CancellationToken ct = default)
    {
        var failedRelay = await _failedRelays.GetByIdAsync(id, ct);
        if (failedRelay is null)
            return Result.NotFound("Retry cache record not found.");

        _failedRelays.Remove(failedRelay);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Ok();
    }

    public async Task<Result<AdminFailedRelayDetail>> GetFailedRelayDetailAsync(Guid id, CancellationToken ct = default)
    {
        var failedRelay = await _failedRelays.GetByIdAsync(id, ct);
        if (failedRelay is null)
            return Result<AdminFailedRelayDetail>.NotFound("Retry cache record not found.");

        var user = await _users.GetByIdAsync(failedRelay.UserId, ct);

        return Result<AdminFailedRelayDetail>.Ok(new AdminFailedRelayDetail(
            failedRelay.Id, failedRelay.UserId, user?.Username ?? "(deleted user)",
            failedRelay.Provider, failedRelay.Status, failedRelay.RetryCount,
            Mappers.ToUnix(failedRelay.NextRetryAt), failedRelay.LastError, failedRelay.TracksJson,
            Mappers.ToUnix(failedRelay.CreatedAt), Mappers.ToUnix(failedRelay.UpdatedAt)));
    }

    public async Task<Result<int>> RetryAllFailedAsync(CancellationToken ct = default)
    {
        var count = await _failedRelays.ResetAllFailedAsync(ct);
        return Result<int>.Ok(count);
    }
}
