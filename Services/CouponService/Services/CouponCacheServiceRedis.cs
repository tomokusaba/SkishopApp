using System.Text.Json;
using CouponService.Configurations;
using CouponService.Models;
using CouponService.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CouponService.Services;

public class CouponCacheServiceRedis(
    IConnectionMultiplexer redis,
    IOptions<CouponSettings> settings,
    IMemoryCache memoryCache,
    ILogger<CouponCacheServiceRedis> logger) : ICouponCacheService
{
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly CacheSettings _cacheSettings = settings.Value.Cache;
    private static readonly TimeSpan FallbackTtl = TimeSpan.FromMinutes(5);

    public async Task<Coupon?> GetCouponAsync(string code, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var cached = await _db.StringGetAsync($"coupon:{code}");
            if (!cached.HasValue) return null;

            logger.LogDebug("キャッシュヒット: coupon:{CouponCode}", code);
            return JsonSerializer.Deserialize<Coupon>(cached.ToString());
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis エラー — InMemory フォールバック: coupon:{CouponCode}", code);
            return memoryCache.TryGetValue($"coupon:{code}", out Coupon? fallback) ? fallback : null;
        }
    }

    public async Task SetCouponAsync(string code, Coupon coupon, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            await _db.StringSetAsync($"coupon:{code}",
                JsonSerializer.Serialize(coupon),
                TimeSpan.FromMinutes(_cacheSettings.CouponTtlMinutes));
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis 書き込みエラー — InMemory フォールバック: coupon:{CouponCode}", code);
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = FallbackTtl
            };
            memoryCache.Set($"coupon:{code}", coupon, cacheOptions);
        }
    }

    public async Task InvalidateAsync(string code, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            await _db.KeyDeleteAsync($"coupon:{code}");
            logger.LogDebug("キャッシュ無効化: coupon:{CouponCode}", code);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis 削除エラー: coupon:{CouponCode}", code);
        }
        memoryCache.Remove($"coupon:{code}");
    }

    public async Task<int?> GetUserUsageCountAsync(string couponId, string userId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var cached = await _db.StringGetAsync($"coupon:usage:{couponId}:{userId}");
            return cached.HasValue ? (int?)int.Parse(cached.ToString()) : null;
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis エラー — InMemory フォールバック: usage");
            return memoryCache.TryGetValue($"coupon:usage:{couponId}:{userId}", out int fallbackCount)
                ? fallbackCount : null;
        }
    }

    public async Task SetUserUsageCountAsync(string couponId, string userId, int count, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            await _db.StringSetAsync($"coupon:usage:{couponId}:{userId}",
                count.ToString(),
                TimeSpan.FromMinutes(_cacheSettings.UsageTtlMinutes));
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis 書き込みエラー — InMemory フォールバック: usage");
            memoryCache.Set($"coupon:usage:{couponId}:{userId}", count,
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = FallbackTtl });
        }
    }
}
