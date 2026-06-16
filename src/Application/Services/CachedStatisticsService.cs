using Microsoft.Extensions.Caching.Memory;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Common;
using Scrobblint.Shared.Stats;

namespace Scrobblint.Application.Services;

/// <summary>
/// Decorates <see cref="IStatisticsService"/> with a sliding-expiration memory cache, keyed by the
/// subject's user id so that scrobble writers can invalidate it the moment new listens arrive.
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

    public async Task<Result<StatsResponse>> GetStatsAsync(string username, ViewerContext viewer, CancellationToken cancellationToken = default)
    {
        // Resolve the subject so the cache key is the user id (writers invalidate by id). For an
        // unknown user, let the inner service produce the canonical not-found result, uncached.
        var user = await _users.GetByUsernameAsync(username, cancellationToken);
        if (user is null || user.IsDisabled)
            return await _inner.GetStatsAsync(username, viewer, cancellationToken);

        return (await _cache.GetOrCreateAsync(CacheKeys.Stats(user.Id), async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(3);
            return await _inner.GetStatsAsync(username, viewer, cancellationToken);
        }))!;
    }
}
