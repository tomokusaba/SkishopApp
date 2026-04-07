using System.Diagnostics.Metrics;

namespace AuthService.Infrastructure.Metrics;

/// <summary>
/// 認証サービスのメトリクス定義を提供する静的クラス。
/// </summary>
/// <remarks>
/// <para>
/// このクラスは、OpenTelemetry の Metrics API を使用して認証関連のメトリクスを定義します。
/// 定義されたメトリクスは、Prometheus などの監視システムによって収集され、
/// ダッシュボードやアラートに使用されます。
/// </para>
/// <para>
/// <strong>メトリクス命名規約:</strong>
/// <list type="bullet">
///   <item>プレフィックス: <c>auth_</c>（認証サービスを識別）</item>
///   <item>サフィックス: <c>_total</c>（カウンターメトリクス）</item>
///   <item>区切り文字: アンダースコア（<c>_</c>）</item>
/// </list>
/// </para>
/// <para>
/// <strong>セキュリティ監視:</strong>
/// これらのメトリクスは、セキュリティインシデントの検知に活用されます。
/// <list type="bullet">
///   <item><see cref="LoginAttempts"/> - ブルートフォース攻撃の検知</item>
///   <item><see cref="AccountLockouts"/> - 大規模な攻撃キャンペーンの検知</item>
///   <item><see cref="MfaVerifications"/> - MFA バイパス試行の検知</item>
/// </list>
/// </para>
/// <para>
/// <strong>アラート設定例:</strong>
/// <list type="bullet">
///   <item><c>rate(auth_login_attempts_total{result="failure"}[5m]) > 100</c> - ログイン失敗の急増</item>
///   <item><c>rate(auth_account_lockouts_total[1h]) > 50</c> - アカウントロックアウトの急増</item>
/// </list>
/// </para>
/// </remarks>
public sealed class AuthMetrics
{
    /// <summary>
    /// 認証サービスのメーター。
    /// </summary>
    /// <remarks>
    /// <para>
    /// メーター名: <c>SkiShop.AuthService</c>
    /// バージョン: <c>1.0.0</c>
    /// </para>
    /// <para>
    /// このメーターは Program.cs で OpenTelemetry の MeterProvider に登録されます。
    /// </para>
    /// </remarks>
    public static readonly Meter Meter = new("SkiShop.AuthService", "1.0.0");

    /// <summary>
    /// ログイン試行回数のカウンター。
    /// </summary>
    /// <remarks>
    /// <para>
    /// メトリクス名: <c>auth_login_attempts_total</c>
    /// </para>
    /// <para>
    /// <strong>ラベル（タグ）:</strong>
    /// <list type="bullet">
    ///   <item><term>result</term><description><c>success</c> または <c>failure</c></description></item>
    ///   <item><term>method</term><description><c>password</c> または <c>oauth</c></description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>使用例:</strong>
    /// <code>
    /// AuthMetrics.LoginAttempts.Add(1, new KeyValuePair&lt;string, object?&gt;("result", "success"),
    ///     new KeyValuePair&lt;string, object?&gt;("method", "password"));
    /// </code>
    /// </para>
    /// </remarks>
    public static readonly Counter<long> LoginAttempts =
        Meter.CreateCounter<long>(
            "auth_login_attempts_total",
            description: "ログイン試行回数（result: success/failure, method: password/oauth）");

    /// <summary>
    /// トークンリフレッシュ回数のカウンター。
    /// </summary>
    /// <remarks>
    /// <para>
    /// メトリクス名: <c>auth_token_refresh_total</c>
    /// </para>
    /// <para>
    /// リフレッシュトークンを使用してアクセストークンを更新した回数を追跡します。
    /// 異常に高い頻度のリフレッシュは、トークン漏洩の兆候である可能性があります。
    /// </para>
    /// </remarks>
    public static readonly Counter<long> TokenRefreshes =
        Meter.CreateCounter<long>(
            "auth_token_refresh_total",
            description: "トークンリフレッシュ回数");

    /// <summary>
    /// MFA 検証回数のカウンター。
    /// </summary>
    /// <remarks>
    /// <para>
    /// メトリクス名: <c>auth_mfa_verification_total</c>
    /// </para>
    /// <para>
    /// <strong>ラベル（タグ）:</strong>
    /// <list type="bullet">
    ///   <item><term>result</term><description><c>success</c> または <c>failure</c></description></item>
    /// </list>
    /// </para>
    /// <para>
    /// MFA 検証の失敗率が高い場合、ユーザーの MFA デバイスの問題や
    /// 攻撃者による総当たり試行の可能性があります。
    /// </para>
    /// </remarks>
    public static readonly Counter<long> MfaVerifications =
        Meter.CreateCounter<long>(
            "auth_mfa_verification_total",
            description: "MFA 検証回数（result: success/failure）");

    /// <summary>
    /// アカウントロックアウト回数のカウンター。
    /// </summary>
    /// <remarks>
    /// <para>
    /// メトリクス名: <c>auth_account_lockouts_total</c>
    /// </para>
    /// <para>
    /// アカウントがロックアウトされた回数を追跡します。
    /// 急激な増加は、大規模なブルートフォース攻撃キャンペーンを示唆する可能性があります。
    /// </para>
    /// </remarks>
    public static readonly Counter<long> AccountLockouts =
        Meter.CreateCounter<long>(
            "auth_account_lockouts_total",
            description: "アカウントロックアウト回数");

    /// <summary>
    /// パスワードリセット回数のカウンター。
    /// </summary>
    /// <remarks>
    /// <para>
    /// メトリクス名: <c>auth_password_resets_total</c>
    /// </para>
    /// <para>
    /// パスワードリセットのリクエスト数を追跡します。
    /// 急激な増加は、フィッシング攻撃やパスワードリセットの悪用を示唆する可能性があります。
    /// </para>
    /// </remarks>
    public static readonly Counter<long> PasswordResets =
        Meter.CreateCounter<long>(
            "auth_password_resets_total",
            description: "パスワードリセット回数");

    /// <summary>
    /// ユーザー登録試行回数のカウンター。
    /// </summary>
    /// <remarks>
    /// <para>
    /// メトリクス名: <c>auth_registration_attempts_total</c>
    /// </para>
    /// <para>
    /// ユーザー登録の試行回数を追跡します。
    /// 異常に高い登録率は、ボットによるアカウント大量作成を示唆する可能性があります。
    /// </para>
    /// </remarks>
    public static readonly Counter<long> RegistrationAttempts =
        Meter.CreateCounter<long>(
            "auth_registration_attempts_total",
            description: "ユーザー登録試行回数");
}
