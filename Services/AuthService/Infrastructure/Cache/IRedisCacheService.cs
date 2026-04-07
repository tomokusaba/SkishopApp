namespace AuthService.Infrastructure.Cache;

/// <summary>
/// Redis キャッシュ操作を抽象化したサービスインターフェース。
/// </summary>
/// <remarks>
/// <para>
/// このインターフェースは、認証サービス全体で使用されるキャッシュ操作を定義します。
/// 主にトークンのブラックリスト管理、セッションキャッシュ、レート制限カウンターなどに使用されます。
/// </para>
/// <para>
/// <strong>実装:</strong>
/// <see cref="RedisCacheService"/> - StackExchange.Redis を使用した Redis 実装
/// </para>
/// <para>
/// <strong>キャッシュ戦略:</strong>
/// <list type="bullet">
///   <item>すべてのキャッシュエントリには有効期限（TTL）が設定されます</item>
///   <item>JSON シリアライゼーションによるオブジェクトの保存をサポート</item>
///   <item>原子的なインクリメント操作をサポート（レート制限用）</item>
/// </list>
/// </para>
/// </remarks>
public interface IRedisCacheService
{
    /// <summary>
    /// 指定されたキーに関連付けられた値を取得します。
    /// </summary>
    /// <typeparam name="T">取得する値の型。</typeparam>
    /// <param name="key">キャッシュキー。</param>
    /// <param name="ct">キャンセルを通知するトークン。</param>
    /// <returns>
    /// キャッシュされた値。キーが存在しない場合は <typeparamref name="T"/> のデフォルト値。
    /// </returns>
    /// <remarks>
    /// <para>
    /// 値は JSON からデシリアライズされます。キャッシュミスの場合はデフォルト値を返します。
    /// </para>
    /// </remarks>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// 指定されたキーに値を設定します。
    /// </summary>
    /// <typeparam name="T">保存する値の型。</typeparam>
    /// <param name="key">キャッシュキー。</param>
    /// <param name="value">保存する値。</param>
    /// <param name="expiration">キャッシュの有効期限。</param>
    /// <param name="ct">キャンセルを通知するトークン。</param>
    /// <returns>処理完了を表す非同期タスク。</returns>
    /// <remarks>
    /// <para>
    /// 値は JSON にシリアライズされて保存されます。
    /// 同じキーに既存の値がある場合は上書きされます。
    /// </para>
    /// </remarks>
    Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken ct = default);

    /// <summary>
    /// 指定されたキーを削除します。
    /// </summary>
    /// <param name="key">削除するキャッシュキー。</param>
    /// <param name="ct">キャンセルを通知するトークン。</param>
    /// <returns>処理完了を表す非同期タスク。</returns>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// 指定されたキーが存在するかどうかを確認します。
    /// </summary>
    /// <param name="key">確認するキャッシュキー。</param>
    /// <param name="ct">キャンセルを通知するトークン。</param>
    /// <returns>キーが存在する場合は true、それ以外は false。</returns>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// 指定されたキーの値をインクリメントします。
    /// </summary>
    /// <param name="key">インクリメントするキャッシュキー。</param>
    /// <param name="expiration">キーが存在しない場合に設定する有効期限。</param>
    /// <param name="ct">キャンセルを通知するトークン。</param>
    /// <returns>インクリメント後の値。</returns>
    /// <remarks>
    /// <para>
    /// キーが存在しない場合は値 1 で作成されます。
    /// <paramref name="expiration"/> が指定され、かつ値が 1 の場合（新規作成時）のみ
    /// 有効期限が設定されます。
    /// </para>
    /// <para>
    /// <strong>主な用途:</strong>
    /// <list type="bullet">
    ///   <item>レート制限カウンター</item>
    ///   <item>ログイン試行回数の追跡</item>
    /// </list>
    /// </para>
    /// </remarks>
    Task<long> IncrementAsync(string key, TimeSpan? expiration = null, CancellationToken ct = default);
}
