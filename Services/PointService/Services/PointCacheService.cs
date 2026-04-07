using System.Text.Json;
using PointService.DTOs.Responses;
using PointService.Services.Interfaces;
using StackExchange.Redis;

namespace PointService.Services;

public class PointCacheService(
    IConnectionMultiplexer redis,
    ILogger<PointCacheService> logger) : IPointCacheService
{
    private static readonly TimeSpan BalanceCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TierCacheTtl = TimeSpan.FromHours(24);

    public async Task<PointBalanceResponse?> GetBalanceCacheAsync(
        string userId, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var cached = await db.StringGetAsync($"point:balance:{userId}");
            if (cached.IsNull) return null;
            return JsonSerializer.Deserialize<PointBalanceResponse>(cached.ToString());
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ取得失敗: UserId={UserId}", userId);
            return null;
        }
    }

    public async Task SetBalanceCacheAsync(
        string userId, PointBalanceResponse balance, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var json = JsonSerializer.Serialize(balance);
            await db.StringSetAsync($"point:balance:{userId}", json, BalanceCacheTtl);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ設定失敗: UserId={UserId}", userId);
        }
    }

    public async Task InvalidateBalanceCacheAsync(
        string userId, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            await db.KeyDeleteAsync($"point:balance:{userId}");
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ削除失敗: UserId={UserId}", userId);
        }
    }

    public async Task<string?> GetUserTierAsync(
        string userId, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var cached = await db.StringGetAsync($"point:tier:{userId}");
            return cached.IsNull ? null : cached.ToString();
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis ティアキャッシュ取得失敗: UserId={UserId}", userId);
            return null;
        }
    }

    public async Task SetUserTierAsync(
        string userId, string tier, decimal pointRate, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            await db.StringSetAsync($"point:tier:{userId}", tier, TierCacheTtl);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis ティアキャッシュ設定失敗: UserId={UserId}", userId);
        }
    }
}
