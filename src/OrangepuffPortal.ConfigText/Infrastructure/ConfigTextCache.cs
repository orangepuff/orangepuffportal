using Microsoft.Extensions.Caching.Distributed;
using OrangepuffPortal.ConfigText.Domain.Entity;
using System.Collections.Concurrent;
using System.Text.Json;

namespace OrangepuffPortal.ConfigText.Infrastructure;

/// <summary>
/// Thin serializable projection of <see cref="ConfigTextDefinition"/> used for Redis serialization —
/// holds only the fields that <see cref="ConfigTextReader"/> needs after a cache hit.
/// </summary>
internal record ConfigTextCacheRow(string Module, string TextCode, string CultureCode, string TextType, string Text);

/// <summary>
/// Caches resolved rows per requested culture in <see cref="IDistributedCache"/> (Redis when
/// <c>ConnectionStrings:Redis</c> is set, otherwise an in-process fallback). A single write can
/// change the "*" fallback row consulted by every culture, so <see cref="InvalidateAsync"/> evicts
/// every cached culture at once.
/// </summary>
/// <remarks>
/// Culture codes that have been loaded are tracked in a per-instance <see cref="ConcurrentDictionary"/>
/// so invalidation can remove those exact keys from Redis. In a multi-instance deployment, an instance
/// that never served a read for a given culture will not clear that culture's Redis key on invalidate;
/// the 24-hour absolute expiration acts as a safety-net TTL in that scenario.
/// </remarks>
internal sealed class ConfigTextCache(IDistributedCache cache)
{
    private static readonly DistributedCacheEntryOptions EntryOptions =
        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) };

    private readonly ConcurrentDictionary<string, bool> _loadedCultures =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<ConfigTextCacheRow>> GetOrCreateAsync(
        string cultureCode,
        Func<Task<IReadOnlyList<ConfigTextCacheRow>>> factory,
        CancellationToken cancellationToken = default)
    {
        var bytes = await cache.GetAsync(CacheKey(cultureCode), cancellationToken);
        if (bytes is not null)
        {
            return JsonSerializer.Deserialize<List<ConfigTextCacheRow>>(bytes)!;
        }

        var rows = await factory();
        await cache.SetAsync(CacheKey(cultureCode), JsonSerializer.SerializeToUtf8Bytes(rows), EntryOptions, cancellationToken);
        _loadedCultures.TryAdd(cultureCode, true);
        return rows;
    }

    public async Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        var cultures = _loadedCultures.Keys.ToArray();
        _loadedCultures.Clear();
        await Task.WhenAll(cultures.Select(c => cache.RemoveAsync(CacheKey(c), cancellationToken)));
        // Speculatively clear the wildcard culture in case it was cached by another instance.
        await cache.RemoveAsync(CacheKey(ConfigTextDefinition.WildcardCulture), cancellationToken);
    }

    private static string CacheKey(string cultureCode) => $"ConfigText:{cultureCode}";
}
