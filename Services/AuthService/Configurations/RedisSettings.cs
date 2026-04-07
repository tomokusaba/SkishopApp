namespace AuthService.Configurations;

/// <summary>
/// Redis 接続設定。
/// セッション管理、リフレッシュトークンの保存、レート制限カウンターに使用される。
/// appsettings.json の "Redis" セクションにバインドされる。
/// IOptions&lt;T&gt; でのバインディングに対応するため init プロパティを使用。
/// </summary>
/// <remarks>
/// <para>Redis の用途:</para>
/// <list type="bullet">
///   <item><description>セッションストア - ユーザーセッション情報の保存</description></item>
///   <item><description>トークンブラックリスト - 無効化されたリフレッシュトークンの追跡</description></item>
///   <item><description>レート制限 - ログイン試行回数の追跡</description></item>
///   <item><description>MFA セッション - 一時的な MFA 認証セッションの保存</description></item>
/// </list>
/// </remarks>
public record RedisSettings
{
    /// <summary>Redis サーバーの接続文字列（例: "localhost:6379" または "redis-server:6379,password=xxx"）。</summary>
    public string ConnectionString { get; init; } = "localhost:6379";

    /// <summary>使用する Redis データベース番号（0-15）。デフォルト: 0。</summary>
    public int DefaultDatabase { get; init; } = 0;

    /// <summary>接続タイムアウト（ミリ秒）。デフォルト: 5000ms。</summary>
    public int ConnectTimeoutMs { get; init; } = 5000;

    /// <summary>同期操作のタイムアウト（ミリ秒）。デフォルト: 1000ms。</summary>
    public int SyncTimeoutMs { get; init; } = 1000;
}
