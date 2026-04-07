using System.Text.Json;
using StackExchange.Redis;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Services;

/// <summary>
/// Redis キャッシュの抽象化層。Redis 接続エラー時は Warning ログを出力し、
/// キャッシュミスとして null を返す（フォールバック戦略）。
/// </summary>
public class CacheService(
    IConnectionMultiplexer redis,
    ILogger<CacheService> logger) : ICacheService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var value = await _db.StringGetAsync(key);
            if (value.IsNullOrEmpty)
                return null;
            return JsonSerializer.Deserialize<T>((string)value!);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis接続エラー（キャッシュミスとして処理）: Key={Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var json = JsonSerializer.Serialize(value);
            if (expiry.HasValue)
                await _db.StringSetAsync(key, json, new Expiration(expiry.Value));
            else
                await _db.StringSetAsync(key, json);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis接続エラー（キャッシュ書き込みスキップ）: Key={Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            await _db.KeyDeleteAsync(key);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis接続エラー（キャッシュ削除スキップ）: Key={Key}", key);
        }
    }
}
