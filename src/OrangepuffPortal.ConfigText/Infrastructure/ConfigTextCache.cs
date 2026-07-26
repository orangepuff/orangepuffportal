using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using OrangepuffPortal.ConfigText.Domain.Entity;

namespace OrangepuffPortal.ConfigText.Infrastructure;

/// <summary>
/// Caches resolved rows per requested culture. A single write can change the "*" fallback row consulted by every culture, so <see cref="Invalidate"/> evicts every cached culture at once via a shared <see cref="CancellationChangeToken"/> rather than tracking/removing individual keys.
/// </summary>
internal sealed class ConfigTextCache(IMemoryCache cache)
{
    private CancellationTokenSource _evictionSource = new();

    public Task<IReadOnlyList<ConfigTextDefinition>> GetOrCreateAsync(string cultureCode, Func<Task<IReadOnlyList<ConfigTextDefinition>>> factory) =>
        cache.GetOrCreateAsync(CacheKey(cultureCode), async entry =>
        {
            entry.AddExpirationToken(new CancellationChangeToken(_evictionSource.Token));
            return await factory();
        })!;

    public void Invalidate()
    {
        var previous = Interlocked.Exchange(ref _evictionSource, new CancellationTokenSource());
        previous.Cancel();
        previous.Dispose();
    }

    private static string CacheKey(string cultureCode) => $"ConfigText:{cultureCode}";
}
