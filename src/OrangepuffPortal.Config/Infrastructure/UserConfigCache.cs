using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OrangepuffPortal.Config.Contract;
using System.Text.Json;

namespace OrangepuffPortal.Config.Infrastructure;

/// <summary>
/// Per-user snapshot cache of <see cref="ConfigUserValueDto"/> lists — populated eagerly at login
/// (see <see cref="UserConfigCacheWarmer"/>) and read synchronously by <see cref="CurrentUserConfig"/>.
/// </summary>
/// <remarks>
/// Uses a two-layer design:
/// <list type="bullet">
///   <item>L1 — <see cref="IMemoryCache"/>: fast synchronous reads within the same process.</item>
///   <item>L2 — <see cref="IDistributedCache"/> (Redis): survives pod restarts and is shared
///   across instances. On an L1 miss, <see cref="TryGet"/> falls back to a synchronous Redis read
///   and re-populates L1 from it. Redis failures are treated as L2 misses and logged as warnings.</item>
/// </list>
/// Sliding expiration on both layers mirrors the auth cookie's own <c>ExpireTimeSpan</c>
/// (<c>PortalBffServiceCollectionExtensions</c>) — a cache entry outlives an idle session for exactly
/// as long as the cookie itself would.
/// </remarks>
internal sealed class UserConfigCache(IMemoryCache memoryCache, IDistributedCache distributedCache, ILogger<UserConfigCache> logger)
{
    private static readonly DistributedCacheEntryOptions RedisOptions =
        new() { SlidingExpiration = TimeSpan.FromHours(8) };

    private static readonly MemoryCacheEntryOptions MemoryOptions =
        new() { SlidingExpiration = TimeSpan.FromHours(8) };

    public bool TryGet(int userId, out IReadOnlyList<ConfigUserValueDto> values)
    {
        if (memoryCache.TryGetValue(CacheKey(userId), out values!))
        {
            return true;
        }

        // L2: synchronous Redis fallback — only on cold start or L1 eviction, so the blocking
        // call here is infrequent and intentional.
        try
        {
            var bytes = distributedCache.Get(CacheKey(userId));
            if (bytes is not null)
            {
                values = JsonSerializer.Deserialize<List<ConfigUserValueDto>>(bytes)!;
                memoryCache.Set(CacheKey(userId), values, MemoryOptions);
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("{LogPrefix}: L2 cache read failed — {ExceptionType}: {Message}",
                nameof(UserConfigCache) + "." + nameof(TryGet), ex.GetType().Name, ex.Message);
        }

        values = [];
        return false;
    }

    public async Task SetAsync(int userId, IReadOnlyList<ConfigUserValueDto> values, CancellationToken cancellationToken = default)
    {
        memoryCache.Set(CacheKey(userId), values, MemoryOptions);

        try
        {
            await distributedCache.SetAsync(
                CacheKey(userId),
                JsonSerializer.SerializeToUtf8Bytes(values),
                RedisOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning("{LogPrefix}: L2 cache write failed — {ExceptionType}: {Message}",
                nameof(UserConfigCache) + "." + nameof(SetAsync), ex.GetType().Name, ex.Message);
        }
    }

    public async Task InvalidateAsync(int userId, CancellationToken cancellationToken = default)
    {
        memoryCache.Remove(CacheKey(userId));

        try
        {
            await distributedCache.RemoveAsync(CacheKey(userId), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning("{LogPrefix}: L2 cache invalidation failed — {ExceptionType}: {Message}",
                nameof(UserConfigCache) + "." + nameof(InvalidateAsync), ex.GetType().Name, ex.Message);
        }
    }

    private static string CacheKey(int userId) => $"UserConfig:{userId}";
}
