namespace AuthService.Configurations;

/// <summary>
/// 認証セキュリティ設定。
/// アカウントロックアウトポリシーや認証失敗カウンターに関する設定を定義する。
/// appsettings.json の "Auth" セクションにバインドされる。
/// IOptions&lt;T&gt; でのバインディングに対応するため init プロパティを使用。
/// </summary>
/// <remarks>
/// <para>ロックアウトの動作:</para>
/// <list type="bullet">
///   <item><description>連続して MaxFailedAttempts 回ログインに失敗するとアカウントがロックされる</description></item>
///   <item><description>ロックは AutoUnlockMinutes 後に自動解除される</description></item>
///   <item><description>認証失敗カウンターは FailedAttemptResetMinutes 経過でリセットされる</description></item>
/// </list>
/// </remarks>
public record AuthSettings
{
    /// <summary>アカウントがロックされるまでの最大認証失敗回数。デフォルト: 5回。</summary>
    public int MaxFailedAttempts { get; init; } = 5;

    /// <summary>アカウントロック後、自動解除されるまでの時間（分）。デフォルト: 30分。</summary>
    public int AutoUnlockMinutes { get; init; } = 30;

    /// <summary>認証失敗カウンターがリセットされるまでの時間（分）。デフォルト: 15分。</summary>
    public int FailedAttemptResetMinutes { get; init; } = 15;
}
