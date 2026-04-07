namespace AuthService.Services.Interfaces;

/// <summary>
/// セキュリティサービスのインターフェース。
/// セキュリティイベントのログ記録、ログイン試行管理、アカウントロック機能を提供します。
/// </summary>
/// <remarks>
/// セキュリティ考慮事項:
/// <list type="bullet">
///   <item>ブルートフォース攻撃対策: 設定された回数のログイン失敗後、アカウントを自動ロックします</item>
///   <item>自動アンロック: 設定された時間経過後、アカウントは自動的にアンロックされます</item>
///   <item>すべてのセキュリティイベントは監査可能な形式でログに記録されます</item>
///   <item>IPアドレスとUser-Agentは不正アクセス検出に使用されます</item>
/// </list>
/// </remarks>
public interface ISecurityService
{
    /// <summary>
    /// セキュリティイベントをログに記録します。
    /// </summary>
    /// <param name="userId">イベントに関連するユーザーのID。未認証イベントの場合は null。</param>
    /// <param name="eventType">イベントタイプ（例: "LOGIN_SUCCESS", "LOGIN_FAILED", "ACCOUNT_LOCKED"）。</param>
    /// <param name="ipAddress">リクエスト元のIPアドレス。</param>
    /// <param name="userAgent">クライアントのUser-Agentヘッダー。</param>
    /// <param name="details">イベントの追加詳細情報。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <param name="saveImmediately">true の場合、即座にデータベースに保存します。false の場合、後続の SaveChanges で保存されます。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eventType"/> が null の場合。</exception>
    /// <remarks>
    /// 自動判定されるフィールド:
    /// <list type="bullet">
    ///   <item>IsSuccess: イベントタイプに "FAILED", "ATTACK", "LOCKED" が含まれていなければ true</item>
    /// </list>
    /// </remarks>
    Task LogSecurityEventAsync(string? userId, string eventType, string? ipAddress,
        string? userAgent, string? details, CancellationToken ct = default, bool saveImmediately = true);

    /// <summary>
    /// ログイン失敗回数をインクリメントし、上限に達した場合はアカウントをロックします。
    /// </summary>
    /// <param name="userId">失敗回数をインクリメントするユーザーのID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>アカウントがロックされた場合は true、そうでない場合は false。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userId"/> が null の場合。</exception>
    /// <exception cref="Exceptions.ConcurrencyException">楽観的ロック競合が発生した場合。</exception>
    /// <remarks>
    /// 動作:
    /// <list type="bullet">
    ///   <item>失敗回数が設定値（MaxFailedAttempts）に達するとアカウントがロックされます</item>
    ///   <item>ロック時にはセキュリティイベント "ACCOUNT_LOCKED" が記録されます</item>
    ///   <item>アカウントロックはメトリクスとして記録されます</item>
    /// </list>
    /// </remarks>
    Task<bool> IncrementFailedAttemptsAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// ログイン失敗回数をリセットします。
    /// </summary>
    /// <param name="userId">リセットするユーザーのID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userId"/> が null の場合。</exception>
    /// <remarks>
    /// 通常、ログイン成功時に呼び出されます。
    /// ユーザーが存在しない場合は何も行いません。
    /// </remarks>
    Task ResetFailedAttemptsAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// アカウントがロックされているかどうかを確認します。
    /// </summary>
    /// <param name="userId">確認するユーザーのID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>アカウントがロックされている場合は true、そうでない場合は false。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userId"/> が null の場合。</exception>
    /// <remarks>
    /// 自動アンロック:
    /// <list type="bullet">
    ///   <item>設定された時間（AutoUnlockMinutes）が経過している場合、自動的にアンロックします</item>
    ///   <item>自動アンロック後は false を返します</item>
    /// </list>
    /// </remarks>
    Task<bool> IsAccountLockedAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// アカウントのロックを解除します。
    /// </summary>
    /// <param name="userId">アンロックするユーザーのID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userId"/> が null の場合。</exception>
    /// <remarks>
    /// 実行される操作:
    /// <list type="bullet">
    ///   <item>ロック状態を解除します</item>
    ///   <item>ログイン失敗回数を0にリセットします</item>
    ///   <item>ロック日時をクリアします</item>
    ///   <item>セキュリティイベント "ACCOUNT_UNLOCKED" が記録されます</item>
    /// </list>
    /// </remarks>
    Task UnlockAccountAsync(string userId, CancellationToken ct = default);
}
