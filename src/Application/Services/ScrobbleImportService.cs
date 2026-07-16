using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Scrobblint.Application.Abstractions;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Abstractions.Relay;
using Scrobblint.Application.Common;
using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;
using Scrobblint.Shared.Connections;

namespace Scrobblint.Application.Services;

public sealed class ScrobbleImportService : IScrobbleImportService
{
    /// <summary>Last.fm's maximum page size for user.getRecentTracks.</summary>
    public const int PageSize = 200;

    private readonly IScrobbleImportRepository _imports;
    private readonly IExternalConnectionRepository _connections;
    private readonly IScrobbleRepository _scrobbles;
    private readonly IUserSettingsRepository _settings;
    private readonly ILastfmRelay _lastfm;
    private readonly IScrobbleImportQueue _queue;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ScrobbleImportService> _logger;

    public ScrobbleImportService(
        IScrobbleImportRepository imports,
        IExternalConnectionRepository connections,
        IScrobbleRepository scrobbles,
        IUserSettingsRepository settings,
        ILastfmRelay lastfm,
        IScrobbleImportQueue queue,
        IUnitOfWork unitOfWork,
        IClock clock,
        IMemoryCache cache,
        ILogger<ScrobbleImportService> logger)
    {
        _imports = imports;
        _connections = connections;
        _scrobbles = scrobbles;
        _settings = settings;
        _lastfm = lastfm;
        _queue = queue;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<ImportStatusDto>> StartLastfmImportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var active = await _imports.GetActiveForUserAsync(userId, cancellationToken);
        if (active is not null)
            return Result<ImportStatusDto>.Ok(Map(active)); // already in progress — idempotent

        var connection = await _connections.GetAsync(userId, ScrobbleProvider.Lastfm, cancellationToken);
        if (connection is null)
            return Result<ImportStatusDto>.Fail(ResultError.Validation, "Connect your Last.fm account first.");
        if (string.IsNullOrWhiteSpace(connection.ExternalUsername))
            return Result<ImportStatusDto>.Fail(ResultError.Validation, "This Last.fm connection has no username; reconnect it.");

        var now = _clock.UtcNow;
        var import = new ScrobbleImport
        {
            UserId = userId,
            Provider = ScrobbleProvider.Lastfm,
            Status = ImportStatus.Pending,
            SourceAccount = connection.ExternalUsername!,
            ToTimestamp = Mappers.ToUnix(now),
            NextPage = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _imports.AddAsync(import, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _queue.Enqueue(import.Id);
        _logger.LogInformation("Started Last.fm import {ImportId} for user {UserId} (account {Account})",
            import.Id, userId, import.SourceAccount);

        return Result<ImportStatusDto>.Ok(Map(import));
    }

    public async Task<ImportStatusDto?> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var import = await _imports.GetLatestForUserAsync(userId, cancellationToken);
        return import is null ? null : Map(import);
    }

    public async Task<Result> CancelAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var active = await _imports.GetActiveForUserAsync(userId, cancellationToken);
        if (active is null)
            return Result.NotFound("No import is currently running.");

        active.Status = ImportStatus.Cancelled;
        active.UpdatedAt = _clock.UtcNow;
        _imports.Update(active);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<bool> ProcessNextChunkAsync(Guid importId, CancellationToken cancellationToken = default)
    {
        var import = await _imports.GetByIdAsync(importId, cancellationToken);
        if (import is null || !import.IsActive)
            return false; // nothing to do (missing, completed, failed or cancelled)

        if (import.Status == ImportStatus.Pending)
            import.Status = ImportStatus.Running;

        var result = await _lastfm.GetRecentTracksAsync(
            import.SourceAccount, import.NextPage, PageSize, import.ToTimestamp, cancellationToken);

        if (!result.Success)
        {
            import.Status = ImportStatus.Failed;
            import.Error = result.Error;
            import.UpdatedAt = _clock.UtcNow;
            _imports.Update(import);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("Import {ImportId} failed on page {Page}: {Error}", importId, import.NextPage, result.Error);
            return false;
        }

        var page = result.Page!;
        if (import.NextPage == 1)
        {
            import.TotalPages = page.TotalPages;
            import.TotalAvailable = page.Total;
        }

        if (page.Tracks.Count > 0)
            await ImportTracksAsync(import, page.Tracks, cancellationToken);

        import.NextPage += 1;
        import.UpdatedAt = _clock.UtcNow;

        var finished = import.TotalPages <= 0 || import.NextPage > import.TotalPages;
        if (finished)
        {
            import.Status = ImportStatus.Completed;
            import.CompletedAt = _clock.UtcNow;
            _logger.LogInformation("Import {ImportId} completed: {Imported} imported, {Dupes} duplicates",
                importId, import.ImportedCount, import.DuplicateCount);
        }

        _imports.Update(import);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cached statistics only when the whole import finishes, so the completed result
        // shows up immediately. Per-page invalidation would thrash the cache across thousands of
        // pages and force a constant, expensive recompute while the import is running.
        if (finished)
            _cache.Remove(CacheKeys.Stats(import.UserId));

        return !finished;
    }

    private async Task ImportTracksAsync(ScrobbleImport import, IReadOnlyList<RelayTrack> tracks, CancellationToken cancellationToken)
    {
        var minUtc = Mappers.FromUnix(tracks.Min(t => t.ListenedAtUnix));
        var maxUtc = Mappers.FromUnix(tracks.Max(t => t.ListenedAtUnix));
        var existing = await _scrobbles.GetExistingKeysAsync(import.UserId, minUtc, maxUtc, cancellationToken);

        var userSettings = await _settings.GetByUserIdAsync(import.UserId, cancellationToken);

        var now = _clock.UtcNow;
        var toInsert = new List<Scrobble>(tracks.Count);
        var duplicates = 0;
        var ignored = 0;

        foreach (var t in tracks)
        {
            var key = ScrobbleKey.For(t.Artist, t.Track, t.ListenedAtUnix);
            if (!existing.Add(key)) // already in DB, or duplicate within this page
            {
                duplicates++;
                continue;
            }

            if (userSettings is not null &&
                ScrobbleIgnoreFilter.ShouldIgnore(
                    t.Artist, t.Track, t.Album,
                    userSettings.ArtistIgnoreRegex,
                    userSettings.TrackIgnoreRegex,
                    userSettings.AlbumIgnoreRegex))
            {
                ignored++;
                continue;
            }

            toInsert.Add(new Scrobble
            {
                UserId = import.UserId,
                Artist = t.Artist,
                Track = t.Track,
                Album = string.IsNullOrWhiteSpace(t.Album) || t.Album.Trim() == "?" ? null : t.Album.Trim(),
                Timestamp = Mappers.FromUnix(t.ListenedAtUnix),
                CreatedAt = now
            });
        }

        if (toInsert.Count > 0)
            await _scrobbles.AddRangeAsync(toInsert, cancellationToken);

        import.ImportedCount += toInsert.Count;
        import.DuplicateCount += duplicates;

        if (ignored > 0)
            _logger.LogInformation("Dropped {Count} imported scrobble(s) matching ignore rules for user {UserId}", ignored, import.UserId);
    }

    private ImportStatusDto Map(ScrobbleImport i) => new(
        i.Status.ToString(),
        i.ImportedCount,
        i.DuplicateCount,
        i.TotalAvailable,
        i.NextPage,
        i.TotalPages,
        i.Error,
        Mappers.ToUnix(i.UpdatedAt));
}
