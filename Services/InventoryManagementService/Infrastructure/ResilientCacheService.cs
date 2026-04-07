using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace InventoryManagementService.Infrastructure;

/// <summary>
/// Redis キャッシュのレジリエントラッパー。障害時にグレースフルデグラデーションを行う。
/// H-14: 各 Service の個別キャッシュ try-catch を共通化。
/// </summary>
public class ResilientCacheService(
    IDistributedCache cache,
    ILogger<ResilientCacheService> logger) : IResilientCacheService
{
    /// <inheritdoc />
    public async Task<string?> GetSafeAsync(string key, CancellationToken ct = default)
    {
        try
        {
            return await cache.GetStringAsync(key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ読み取りエラー（フォールバック）: Key={Key}", key);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetSafeAsync<T>(string key, T value, int ttlSeconds, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await cache.SetStringAsync(key, json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
                }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ書き込みエラー（無視）: Key={Key}", key);
        }
    }

    /// <inheritdoc />
    public async Task RemoveSafeAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveAsync(key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ削除エラー（無視）: Key={Key}", key);
        }
    }
}
