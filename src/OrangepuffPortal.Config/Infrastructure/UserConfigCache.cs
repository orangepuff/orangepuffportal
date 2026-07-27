using Microsoft.Extensions.Caching.Memory;
using OrangepuffPortal.Config.Contract;

namespace OrangepuffPortal.Config.Infrastructure;

/// <summary>
/// Per-user snapshot cache of <see cref="ConfigUserValueDto"/> lists — populated eagerly at login (see
/// <see cref="UserConfigCacheWarmer"/>) and read synchronously by <see cref="CurrentUserConfig"/>.
/// Sliding expiration matches the auth cookie's own <c>ExpireTimeSpan</c>
/// (<c>PortalBffServiceCollectionExtensions</c>) — a cache entry outlives an idle session for exactly
/// as long as the cookie itself would.
/// </summary>
internal sealed class UserConfigCache(IMemoryCache cache)
{
    private static readonly MemoryCacheEntryOptions EntryOptions = new() { SlidingExpiration = TimeSpan.FromHours(8) };

    public bool TryGet(int userId, out IReadOnlyList<ConfigUserValueDto> values) =>
        cache.TryGetValue(CacheKey(userId), out values!);

    public void Set(int userId, IReadOnlyList<ConfigUserValueDto> values) =>
        cache.Set(CacheKey(userId), values, EntryOptions);

    public void Invalidate(int userId) => cache.Remove(CacheKey(userId));

    private static string CacheKey(int userId) => $"UserConfig:{userId}";
}
