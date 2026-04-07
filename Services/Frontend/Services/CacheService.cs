using System.Collections.Concurrent;
using Frontend.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Frontend.Services;

/// <summary>
/// データ種別ごとのキャッシュ戦略を提供するキャッシュサービス（§6.2）
/// H-16: InvalidateByPrefix を ConcurrentDictionary + PostEvictionCallback で実装
/// </summary>
public class CacheService(IMemoryCache cache, ILogger<CacheService> logger) : ICacheService
{
    private readonly ConcurrentDictionary<string, byte> _trackedKeys = new();

    private static readonly Dictionary<string, TimeSpan> CacheDurations = new()
    {
        ["products"] = TimeSpan.FromMinutes(5),
        ["categories"] = TimeSpan.FromMinutes(10),
        ["cart"] = TimeSpan.Zero,
        ["orders"] = TimeSpan.FromMinutes(1),
        ["points"] = TimeSpan.FromSeconds(30),
        ["coupons"] = TimeSpan.FromMinutes(5),
        ["user_profile"] = TimeSpan.FromMinutes(2),
        ["wishlists"] = TimeSpan.FromMinutes(1),
    };

    public async Task<T?> GetOrSetAsync<T>(
        string key,
        string dataType,
        Func<Task<T?>> factory,
        CancellationToken ct = default) where T : class
    {
        if (!CacheDurations.TryGetValue(dataType, out var duration) || duration == TimeSpan.Zero)
        {
            return await factory();
        }

        if (cache.TryGetValue(key, out T? cached))
        {
            logger.LogDebug("Cache hit: {CacheKey}", key);
            return cached;
        }

        logger.LogDebug("Cache miss: {CacheKey}", key);
        var result = await factory();
        if (result is not null)
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = duration
            };
            options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
            {
                _trackedKeys.TryRemove(evictedKey.ToString()!, out _);
            });
            cache.Set(key, result, options);
            _trackedKeys.TryAdd(key, 0);
        }

        return result;
    }

    public void Invalidate(string key)
    {
        cache.Remove(key);
        _trackedKeys.TryRemove(key, out _);
        logger.LogDebug("Cache invalidated: {CacheKey}", key);
    }

    public void InvalidateByPrefix(string prefix)
    {
        var keysToRemove = _trackedKeys.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            cache.Remove(key);
            _trackedKeys.TryRemove(key, out _);
        }

        logger.LogDebug("Cache prefix invalidation: Prefix={Prefix}, RemovedCount={Count}", prefix, keysToRemove.Count);
    }
}
