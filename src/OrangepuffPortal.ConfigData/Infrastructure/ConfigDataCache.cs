using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace OrangepuffPortal.ConfigData.Infrastructure;

/// <summary>
/// Thin serializable projection of <see cref="Domain.Entity.ConfigData"/> used for Redis serialization.
/// </summary>
internal record ConfigDataCacheRow(int Id, string Key, string? Value, bool? AllowEditByScreen, string? Description);

/// <summary>
/// Caches all ConfigData rows as a single <c>ConfigData:all</c> key in <see cref="IDistributedCache"/>
/// (Redis when configured, otherwise an in-process fallback). The entire key is invalidated on any
/// admin write and re-populated on the next read. A 24-hour absolute TTL acts as a safety net.
/// </summary>
internal sealed class ConfigDataCache(IDistributedCache distributedCache)
{
    internal const string CacheKey = "ConfigData:all";

    private static readonly DistributedCacheEntryOptions EntryOptions =
        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) };

    public async Task<IReadOnlyList<ConfigDataCacheRow>?> GetAsync(CancellationToken cancellationToken = default)
    {
        var bytes = await distributedCache.GetAsync(CacheKey, cancellationToken);
        return bytes is null ? null : JsonSerializer.Deserialize<List<ConfigDataCacheRow>>(bytes);
    }

    public async Task SetAllAsync(IReadOnlyList<ConfigDataCacheRow> rows, CancellationToken cancellationToken = default) =>
        await distributedCache.SetAsync(CacheKey, JsonSerializer.SerializeToUtf8Bytes(rows), EntryOptions, cancellationToken);

    public async Task InvalidateAsync(CancellationToken cancellationToken = default) =>
        await distributedCache.RemoveAsync(CacheKey, cancellationToken);
}
