namespace InventoryManagementService.Infrastructure;

/// <summary>
/// Redis キャッシュのレジリエントラッパーインターフェース。
/// 障害時にグレースフルデグラデーションを行い、例外を上位にスローしない。
/// </summary>
public interface IResilientCacheService
{
    /// <summary>
    /// キャッシュ値を安全に取得する。Redis 障害時は null を返す。
    /// </summary>
    /// <param name="key">キャッシュキー</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>キャッシュ値。取得失敗時は null</returns>
    Task<string?> GetSafeAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// キャッシュ値を安全に設定する。Redis 障害時はログ出力のみで例外をスローしない。
    /// </summary>
    /// <typeparam name="T">キャッシュ対象の型</typeparam>
    /// <param name="key">キャッシュキー</param>
    /// <param name="value">キャッシュ値</param>
    /// <param name="ttlSeconds">TTL（秒）</param>
    /// <param name="ct">キャンセルトークン</param>
    Task SetSafeAsync<T>(string key, T value, int ttlSeconds, CancellationToken ct = default);

    /// <summary>
    /// キャッシュを安全に削除する。Redis 障害時はログ出力のみで例外をスローしない。
    /// </summary>
    /// <param name="key">キャッシュキー</param>
    /// <param name="ct">キャンセルトークン</param>
    Task RemoveSafeAsync(string key, CancellationToken ct = default);
}
