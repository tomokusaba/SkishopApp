namespace AuthService.Configurations;

/// <summary>
/// ユーザーセッション設定。
/// セッションの有効期限と同時接続制限を定義する。
/// appsettings.json の "Session" セクションにバインドされる。
/// IOptions&lt;T&gt; でのバインディングに対応するため init プロパティを使用。
/// </summary>
/// <remarks>
/// <para>セッション管理の動作:</para>
/// <list type="bullet">
///   <item><description>セッションは Timeout 秒間アクティビティがないと期限切れになる</description></item>
///   <item><description>同一ユーザーの同時セッション数は MaxConcurrentSessions に制限される</description></item>
///   <item><description>上限を超える新規ログイン時、最も古いセッションが無効化される</description></item>
/// </list>
/// </remarks>
public record SessionSettings
{
    /// <summary>セッションの無活動タイムアウト（秒）。デフォルト: 1800秒（30分）。</summary>
    public int Timeout { get; init; } = 1800;

    /// <summary>ユーザーあたりの最大同時セッション数。デフォルト: 5。</summary>
    public int MaxConcurrentSessions { get; init; } = 5;
}
