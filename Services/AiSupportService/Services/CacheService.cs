using System.Text.Json;
using AiSupportService.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace AiSupportService.Services;

/// <summary>
/// <see cref="ICacheService"/> の実装。<see cref="IDistributedCache"/> を使用して JSON シリアライズベースのキャッシュを提供する。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、Redis などの分散キャッシュシステムへのアクセスを提供します。
/// 値は <see cref="JsonSerializer"/> を使用して JSON 形式でシリアライズされます。
/// </para>
/// <para>
/// エラーハンドリング:
/// キャッシュ操作が失敗した場合でも、例外はスローされません。
/// エラーは警告としてログに記録され、キャッシュミスとして扱われます。
/// これにより、キャッシュ障害がアプリケーション全体の可用性に影響しないようにします。
/// </para>
/// <para>
/// デフォルトの有効期限は 10 分です。
/// </para>
/// </remarks>
/// <param name="cache">分散キャッシュインスタンス。</param>
/// <param name="logger">ロガー。</param>
public class CacheService(
    IDistributedCache cache,
    ILogger<CacheService> logger) : ICacheService
{
    /// <summary>
    /// デフォルトのキャッシュ有効期限（10 分）。
    /// </summary>
    /// <remarks>
    /// この値は、検索結果やレコメンデーションなど、
    /// 適度に新鮮であることが求められるデータに適しています。
    /// </remarks>
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(10);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// キャッシュからの取得処理:
    /// <list type="number">
    ///   <item><description>指定キーで文字列データを取得</description></item>
    ///   <item><description>データが存在する場合、JSON をデシリアライズして返却</description></item>
    ///   <item><description>データが存在しない場合、<c>null</c> を返却</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// デシリアライズエラーや接続エラーが発生した場合は、
    /// 警告ログが出力され <c>null</c> が返されます。
    /// </para>
    /// </remarks>
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var data = await cache.GetStringAsync(key, ct);
            if (data is null)
                return null;

            logger.LogDebug("キャッシュヒット: Key={CacheKey}", key);
            return JsonSerializer.Deserialize<T>(data);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "キャッシュ取得エラー: Key={CacheKey}", key);
            return null;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// キャッシュへの設定処理:
    /// <list type="number">
    ///   <item><description>値を JSON 形式にシリアライズ</description></item>
    ///   <item><description>有効期限オプションを設定（デフォルト: 10 分）</description></item>
    ///   <item><description>指定キーでキャッシュに保存</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// シリアライズエラーや接続エラーが発生した場合は、
    /// 警告ログが出力されますが、例外はスローされません。
    /// </para>
    /// </remarks>
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        try
        {
            var data = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration
            };
            await cache.SetStringAsync(key, data, options, ct);
            logger.LogDebug("キャッシュ設定: Key={CacheKey}, Expiration={Expiration}", key, expiration ?? DefaultExpiration);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "キャッシュ設定エラー: Key={CacheKey}", key);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// キーが存在しない場合でもエラーは発生しません。
    /// 削除に失敗した場合は警告ログが出力されますが、例外はスローされません。
    /// </remarks>
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveAsync(key, ct);
            logger.LogDebug("キャッシュ削除: Key={CacheKey}", key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "キャッシュ削除エラー: Key={CacheKey}", key);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <see cref="IDistributedCache"/> の標準実装ではプレフィックス検索がサポートされていないため、
    /// 現在の実装ではログ出力のみを行います。
    /// </para>
    /// <para>
    /// Redis を直接使用する場合は、SCAN コマンドでキーを検索し、
    /// 一括削除を行う実装に置き換えることができます。
    /// </para>
    /// </remarks>
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        logger.LogDebug("プレフィックス削除要求: Prefix={Prefix} (IDistributedCache では個別キー削除が必要)", prefix);
    }
}
