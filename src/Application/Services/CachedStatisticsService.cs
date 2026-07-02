using Microsoft.Extensions.Caching.Memory;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Common;
using Scrobblint.Shared.Stats;

namespace Scrobblint.Application.Services;

/// <summary>
/// Decorates <see cref="IStatisticsService"/> with a short absolute-expiration memory cache, keyed
/// by the subject's user id. Over large histories the aggregation is expensive, so it is recomputed
/// at most once per window; absolute (not sliding) expiration also stops a stale entry being kept
/// alive forever by repeated viewing. Writers invalidate the key by id when a result must be fresh
/// immediately (e.g. an import completing).
/// </summary>
public sealed class CachedStatisticsService : IStatisticsService
{
    private readonly IStatisticsService _inner;
    private readonly IUserRepository _users;
    private readonly IMemoryCache _cache;

    public CachedStatisticsService(IStatisticsService inner, IUserRepository users, IMemoryCache cache)
    {
        _inner = inner;
        _users = users;
        _cache = cache;
    }

    public async Task<Result<StatsResponse>> GetStatsAsync(
        string username, ViewerContext viewer,
        DateTime? from = null, DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        // Resolve the subject so the cache key is the user id (writers invalidate by id). For an
        // unknown user, let the inner service produce the canonical not-found result, uncached.
        var user = await _users.GetByUsernameAsync(username, cancellationToken);
        if (user is null || user.IsDisabled)
            return await _inner.GetStatsAsync(username, viewer, from, to, cancellationToken);

        // Date-filtered queries are unique per range — don't pollute or saturate the cache.
        if (from is not null || to is not null)
            return await _inner.GetStatsAsync(username, viewer, from, to, cancellationToken);

        return (await _cache.GetOrCreateAsync(CacheKeys.Stats(user.Id), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return await _inner.GetStatsAsync(username, viewer, cancellationToken: cancellationToken);
        }))!;
    }

    public async Task<Result<GlobalStatsResponse>> GetGlobalStatsAsync(CancellationToken cancellationToken = default)
    {
        return (await _cache.GetOrCreateAsync(CacheKeys.GlobalStats(), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return await _inner.GetGlobalStatsAsync(cancellationToken);
        }))!;
    }
}
