namespace AuthService.DTOs.Responses;

/// <summary>
/// MFA セットアップレスポンス DTO。
/// 多要素認証（MFA）の有効化時に返される設定情報。
/// POST /auth/mfa/setup エンドポイントのレスポンスとして使用される。
/// </summary>
/// <remarks>
/// <para>セットアップフロー:</para>
/// <list type="number">
///   <item><description>本レスポンスの QrCodeUri を QR コードとして表示</description></item>
///   <item><description>ユーザーが認証アプリ（Google Authenticator 等）で QR コードをスキャン</description></item>
///   <item><description>認証アプリで生成されたコードで MFA 有効化を確認</description></item>
///   <item><description>BackupCodes は安全な場所に保存するよう案内（認証アプリ紛失時の回復用）</description></item>
/// </list>
/// </remarks>
/// <param name="SecretKey">TOTP シークレットキー（Base32 形式）。手動入力用。QR コードが読めない場合に使用。</param>
/// <param name="QrCodeUri">TOTP 登録用の URI（otpauth:// 形式）。QR コード生成に使用。</param>
/// <param name="BackupCodes">バックアップコードのリスト。各コードは1回のみ使用可能。認証アプリ紛失時の回復に使用。</param>
public record MfaSetupResponse(
    string SecretKey,
    string QrCodeUri,
    IReadOnlyList<string> BackupCodes);
