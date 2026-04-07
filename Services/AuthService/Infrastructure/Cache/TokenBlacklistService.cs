namespace AuthService.Infrastructure.Cache;

/// <summary>
/// JWT トークンのブラックリスト管理を行うサービスインターフェース。
/// </summary>
/// <remarks>
/// <para>
/// JWT トークンはステートレスな性質上、発行後に無効化することが困難です。
/// このサービスは、ログアウトや権限変更時にトークンを即座に無効化するための
/// ブラックリスト機構を提供します。
/// </para>
/// <para>
/// <strong>セキュリティ上の重要性:</strong>
/// <list type="bullet">
///   <item>ログアウト時のトークン即時無効化</item>
///   <item>権限変更時の古いトークンの無効化</item>
///   <item>トークン漏洩時の緊急対応</item>
/// </list>
/// </para>
/// <para>
/// <strong>実装:</strong>
/// <see cref="TokenBlacklistService"/> - Redis をバックエンドとした実装
/// </para>
/// </remarks>
public interface ITokenBlacklistService
{
    /// <summary>
    /// トークンをブラックリストに追加します。
    /// </summary>
    /// <param name="jti">ブラックリストに追加するトークンの JWT ID（jti クレーム）。</param>
    /// <param name="remaining">トークンの残りの有効期間。この期間が経過するとブラックリストから自動的に削除されます。</param>
    /// <param name="ct">キャンセルを通知するトークン。</param>
    /// <returns>処理完了を表す非同期タスク。</returns>
    /// <remarks>
    /// <para>
    /// ブラックリストエントリの TTL は、トークンの残りの有効期間に設定されます。
    /// これにより、トークンの有効期限後は自動的にブラックリストから削除され、
    /// ストレージを効率的に使用します。
    /// </para>
    /// </remarks>
    Task BlacklistTokenAsync(string jti, TimeSpan remaining, CancellationToken ct = default);

    /// <summary>
    /// トークンがブラックリストに含まれているかどうかを確認します。
    /// </summary>
    /// <param name="jti">確認するトークンの JWT ID（jti クレーム）。</param>
    /// <param name="ct">キャンセルを通知するトークン。</param>
    /// <returns>ブラックリストに含まれている場合は true、それ以外は false。</returns>
    /// <remarks>
    /// <para>
    /// このメソッドは、すべてのリクエストで JWT の検証時に呼び出されるため、
    /// 高いパフォーマンスが求められます。Redis の EXISTS 操作を使用して
    /// 効率的にチェックを行います。
    /// </para>
    /// </remarks>
    Task<bool> IsBlacklistedAsync(string jti, CancellationToken ct = default);
}

/// <summary>
/// <see cref="ITokenBlacklistService"/> の Redis 実装。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、Redis をバックエンドとしてトークンのブラックリストを管理します。
/// 各ブラックリストエントリは、トークンの残りの有効期間をTTLとして設定し、
/// 自動的に期限切れになります。
/// </para>
/// <para>
/// <strong>キー形式:</strong>
/// <c>blacklist:{jti}</c>
/// </para>
/// <para>
/// <strong>パフォーマンス考慮事項:</strong>
/// <list type="bullet">
///   <item>Redis の EXISTS 操作は O(1) で実行されます</item>
///   <item>TTL による自動クリーンアップでストレージを効率的に使用</item>
///   <item>高頻度の呼び出しに対応可能な設計</item>
/// </list>
/// </para>
/// </remarks>
/// <param name="cache">Redis キャッシュサービス。</param>
/// <param name="logger">ログ出力に使用するロガー。</param>
public sealed class TokenBlacklistService(
    IRedisCacheService cache,
    ILogger<TokenBlacklistService> logger) : ITokenBlacklistService
{
    /// <summary>
    /// ブラックリストキーのプレフィックス。
    /// </summary>
    private const string BlacklistPrefix = "blacklist:";

    /// <inheritdoc />
    public async Task BlacklistTokenAsync(string jti, TimeSpan remaining, CancellationToken ct = default)
    {
        await cache.SetAsync($"{BlacklistPrefix}{jti}", true, remaining, ct);
        logger.LogInformation("Token blacklisted: {Jti}", jti);
    }

    /// <inheritdoc />
    public async Task<bool> IsBlacklistedAsync(string jti, CancellationToken ct = default)
        => await cache.ExistsAsync($"{BlacklistPrefix}{jti}", ct);
}
