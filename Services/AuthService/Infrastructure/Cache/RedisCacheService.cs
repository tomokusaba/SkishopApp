using System.Text.Json;
using StackExchange.Redis;

namespace AuthService.Infrastructure.Cache;

/// <summary>
/// <see cref="IRedisCacheService"/> の Redis 実装。
/// </summary>
/// <remarks>
/// <para>
/// このクラスは StackExchange.Redis を使用して Redis との通信を行います。
/// <see cref="IConnectionMultiplexer"/> は DI コンテナで Singleton として登録され、
/// 接続プーリングが自動的に管理されます。
/// </para>
/// <para>
/// <strong>シリアライゼーション:</strong>
/// オブジェクトは <see cref="System.Text.Json.JsonSerializer"/> を使用して JSON に変換されます。
/// デフォルトのシリアライザー設定が使用されます。
/// </para>
/// <para>
/// <strong>エラーハンドリング:</strong>
/// Redis 接続エラーは呼び出し元に伝播されます。クライアントコードは
/// <see cref="RedisConnectionException"/> などの例外をハンドリングする必要があります。
/// </para>
/// <para>
/// <strong>パフォーマンス考慮事項:</strong>
/// <list type="bullet">
///   <item>すべての操作は非同期で実行されます</item>
///   <item><see cref="IConnectionMultiplexer"/> は内部で接続プーリングを管理します</item>
///   <item>大きなオブジェクトのキャッシュは避けてください</item>
/// </list>
/// </para>
/// </remarks>
/// <param name="redis">Redis 接続マルチプレクサ。</param>
/// <param name="logger">ログ出力に使用するロガー。</param>
public sealed class RedisCacheService(
    IConnectionMultiplexer redis,
    ILogger<RedisCacheService> logger) : IRedisCacheService
{
    /// <summary>
    /// Redis データベースへの接続。
    /// </summary>
    private readonly IDatabase _db = redis.GetDatabase();

    /// <inheritdoc />
    /// <remarks>
    /// キャッシュヒット時は Debug レベルでログを出力します。
    /// </remarks>
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(key);
        if (value.IsNullOrEmpty)
        {
            return default;
        }

        logger.LogDebug("Cache hit: {CacheKey}", key);
        return JsonSerializer.Deserialize<T>((string)value!);
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, json, expiration);
        logger.LogDebug("Cache set: {CacheKey}, Expiration={Expiration}", key, expiration);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await _db.KeyDeleteAsync(key);
        logger.LogDebug("Cache removed: {CacheKey}", key);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => await _db.KeyExistsAsync(key);

    /// <inheritdoc />
    public async Task<long> IncrementAsync(string key, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        var result = await _db.StringIncrementAsync(key);
        if (expiration.HasValue && result == 1)
        {
            await _db.KeyExpireAsync(key, expiration.Value);
        }

        return result;
    }
}
