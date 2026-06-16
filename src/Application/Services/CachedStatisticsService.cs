using Microsoft.Extensions.Caching.Memory;
using Scrobblint.Application.Common;
using Scrobblint.Shared.Stats;

namespace Scrobblint.Application.Services;

/// <summary>
/// Decorates <see cref="IStatisticsService"/> with a sliding-expiration memory cache.
/// Visibility enforcement still runs on every call; only the expensive aggregation result
/// is cached per (username, viewer) key for a short window.
/// </summary>
public sealed class CachedStatisticsService : IStatisticsService
{
    private readonly IStatisticsService _inner;
    private readonly IMemoryCache _cache;

    public CachedStatisticsService(IStatisticsService inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public Task<Result<StatsResponse>> GetStatsAsync(string username, ViewerContext viewer, CancellationToken cancellationToken = default)
    {
        var key = $"stats:{username}";

        return _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(3);
            return await _inner.GetStatsAsync(username, viewer, cancellationToken);
        })!;
    }
}
