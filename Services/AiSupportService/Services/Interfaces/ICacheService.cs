namespace AiSupportService.Services.Interfaces;

/// <summary>
/// 分散キャッシュの読み書き操作を抽象化するサービスインターフェース。
/// </summary>
/// <remarks>
/// <para>
/// このインターフェースは、Redis などの分散キャッシュシステムへのアクセスを抽象化し、
/// アプリケーション層からキャッシュの実装詳細を隠蔽します。
/// </para>
/// <para>
/// キャッシュキーの命名規則:
/// <list type="bullet">
///   <item><description>検索結果: <c>search:{query}:{category}:{minPrice}:{maxPrice}:{page}:{pageSize}</c></description></item>
///   <item><description>レコメンデーション: <c>recommendations:{type}:{userId|productId}</c></description></item>
///   <item><description>ユーザープロファイル: <c>user:profile:{userId}</c></description></item>
/// </list>
/// </para>
/// <para>
/// キャッシュ操作が失敗した場合でも、サービスは正常に動作を継続します。
/// エラーは警告としてログに記録され、キャッシュミスとして扱われます。
/// </para>
/// </remarks>
public interface ICacheService
{
    /// <summary>
    /// 指定キーのキャッシュ値を取得する。
    /// </summary>
    /// <typeparam name="T">
    /// キャッシュ値の型。JSON デシリアライズ可能な参照型である必要があります。
    /// </typeparam>
    /// <param name="key">
    /// キャッシュキー。一意の識別子として使用されます。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// キャッシュされた値。キーが存在しない場合、または取得に失敗した場合は <c>null</c>。
    /// </returns>
    /// <remarks>
    /// キャッシュヒット時はデバッグログが出力されます。
    /// デシリアライズエラーや接続エラーが発生した場合は、警告ログが出力され <c>null</c> が返されます。
    /// </remarks>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    /// <summary>
    /// 指定キーにキャッシュ値を設定する。
    /// </summary>
    /// <typeparam name="T">
    /// キャッシュ値の型。JSON シリアライズ可能な参照型である必要があります。
    /// </typeparam>
    /// <param name="key">
    /// キャッシュキー。一意の識別子として使用されます。
    /// </param>
    /// <param name="value">
    /// キャッシュする値。<c>null</c> は許可されません。
    /// </param>
    /// <param name="expiration">
    /// キャッシュの有効期限。<c>null</c> の場合はデフォルト値（10 分）が使用されます。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// キャッシュ設定操作を表すタスク。
    /// </returns>
    /// <remarks>
    /// 値は JSON 形式でシリアライズされて保存されます。
    /// 設定に失敗した場合は警告ログが出力されますが、例外はスローされません。
    /// </remarks>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class;

    /// <summary>
    /// 指定キーのキャッシュを削除する。
    /// </summary>
    /// <param name="key">
    /// 削除するキャッシュのキー。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// キャッシュ削除操作を表すタスク。
    /// </returns>
    /// <remarks>
    /// キーが存在しない場合でもエラーは発生しません。
    /// 削除に失敗した場合は警告ログが出力されますが、例外はスローされません。
    /// </remarks>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// 指定プレフィックスに一致するキャッシュを一括削除する。
    /// </summary>
    /// <param name="prefix">
    /// キャッシュキーのプレフィックス。例: <c>recommendations:personalized:</c>
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// キャッシュ一括削除操作を表すタスク。
    /// </returns>
    /// <remarks>
    /// <para>
    /// <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> の標準実装では
    /// プレフィックス検索がサポートされていないため、この操作は実装に依存します。
    /// </para>
    /// <para>
    /// Redis を使用する場合は SCAN + DEL コマンドで実装可能ですが、
    /// 標準の <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> では
    /// 個別キーの削除が必要です。
    /// </para>
    /// </remarks>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}
