using CouponService.Models;
using CouponService.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace CouponService.Services;

public class CouponCacheServiceInMemory(
    IMemoryCache memoryCache,
    ILogger<CouponCacheServiceInMemory> logger) : ICouponCacheService
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);
    private const string CouponKeyPrefix = "coupon:";
    private const string UserUsageKeyPrefix = "coupon:usage:";

    public Task<Coupon?> GetCouponAsync(string code, CancellationToken ct = default)
    {
        var key = $"{CouponKeyPrefix}{code}";
        if (memoryCache.TryGetValue(key, out Coupon? coupon))
        {
            logger.LogDebug("キャッシュヒット: {CouponCode}", code);
            return Task.FromResult(coupon);
        }

        return Task.FromResult<Coupon?>(null);
    }

    public Task SetCouponAsync(string code, Coupon coupon, CancellationToken ct = default)
    {
        var key = $"{CouponKeyPrefix}{code}";
        memoryCache.Set(key, coupon, DefaultExpiration);
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(string code, CancellationToken ct = default)
    {
        var key = $"{CouponKeyPrefix}{code}";
        memoryCache.Remove(key);
        logger.LogDebug("キャッシュ無効化: {CouponCode}", code);
        return Task.CompletedTask;
    }

    public Task<int?> GetUserUsageCountAsync(string couponId, string userId, CancellationToken ct = default)
    {
        var key = $"{UserUsageKeyPrefix}{couponId}:{userId}";
        if (memoryCache.TryGetValue(key, out int count))
            return Task.FromResult<int?>(count);
        return Task.FromResult<int?>(null);
    }

    public Task SetUserUsageCountAsync(string couponId, string userId, int count, CancellationToken ct = default)
    {
        var key = $"{UserUsageKeyPrefix}{couponId}:{userId}";
        memoryCache.Set(key, count, TimeSpan.FromMinutes(3));
        return Task.CompletedTask;
    }
}
