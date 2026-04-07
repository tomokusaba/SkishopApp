namespace AuthService.Enums;

/// <summary>
/// セキュリティ監査ログに記録されるイベントの種別を表す列挙型。
/// OWASP ログ記録ガイドラインに準拠したセキュリティイベントの分類に使用される。
/// </summary>
/// <remarks>
/// <para>
/// 全てのセキュリティイベントは <see cref="Models.SecurityLog"/> に記録され、
/// 不正アクセスの検知、コンプライアンス監査、インシデント対応に使用される。
/// </para>
/// <para>
/// 注意: ログには PII（個人情報）を含めないこと。IP アドレスとイベント種別のみを記録する。
/// </para>
/// </remarks>
public enum SecurityEventType
{
    /// <summary>
    /// ログイン成功。ユーザー認証が正常に完了した。
    /// </summary>
    LoginSuccess,

    /// <summary>
    /// ログイン失敗。認証情報の不一致またはアカウントロック状態。ブルートフォース検知の対象。
    /// </summary>
    LoginFailed,

    /// <summary>
    /// ログアウト。ユーザーが明示的にセッションを終了した。
    /// </summary>
    Logout,

    /// <summary>
    /// アカウントロック。連続したログイン失敗によりアカウントが自動ロックされた。
    /// </summary>
    AccountLocked,

    /// <summary>
    /// アカウントロック解除。管理者操作またはタイムアウトによりロックが解除された。
    /// </summary>
    AccountUnlocked,

    /// <summary>
    /// パスワード変更。ユーザーがパスワードを正常に変更した。
    /// </summary>
    PasswordChanged,

    /// <summary>
    /// パスワードリセット要求。パスワードリセットメールの送信が要求された。
    /// </summary>
    PasswordResetRequested,

    /// <summary>
    /// パスワードリセット完了。リセットトークンを使用してパスワードが正常に変更された。
    /// </summary>
    PasswordResetCompleted,

    /// <summary>
    /// MFA 有効化。二要素認証がアカウントで有効化された。
    /// </summary>
    MfaEnabled,

    /// <summary>
    /// MFA 無効化。二要素認証がアカウントで無効化された。セキュリティリスク要注意。
    /// </summary>
    MfaDisabled,

    /// <summary>
    /// MFA 検証成功。TOTP コードまたはバックアップコードの検証が成功した。
    /// </summary>
    MfaVerified,

    /// <summary>
    /// MFA 検証失敗。不正な TOTP コードまたはバックアップコードが入力された。
    /// </summary>
    MfaFailed,

    /// <summary>
    /// トークン無効化。リフレッシュトークンが明示的に無効化された。
    /// </summary>
    TokenRevoked,

    /// <summary>
    /// セッション作成。新しいユーザーセッションが開始された。
    /// </summary>
    SessionCreated,

    /// <summary>
    /// セッション期限切れ。セッションがタイムアウトにより終了した。
    /// </summary>
    SessionExpired,

    /// <summary>
    /// OAuth アカウント連携。外部 OAuth プロバイダー（Google, GitHub 等）とのアカウント連携が完了した。
    /// </summary>
    OAuthLinked,

    /// <summary>
    /// OAuth アカウント連携解除。外部 OAuth プロバイダーとの連携が解除された。
    /// </summary>
    OAuthUnlinked,

    /// <summary>
    /// ユーザー登録。新しいユーザーアカウントが作成された。
    /// </summary>
    UserRegistered,

    /// <summary>
    /// ユーザー削除。ユーザーアカウントが削除または無効化された。
    /// </summary>
    UserDeleted,

    /// <summary>
    /// セキュリティインシデント。不正アクセスの疑い、異常なアクティビティ等の重大なセキュリティイベント。
    /// </summary>
    SecurityIncident
}
