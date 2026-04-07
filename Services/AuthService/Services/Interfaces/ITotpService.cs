namespace AuthService.Services.Interfaces;

/// <summary>
/// TOTP（Time-based One-Time Password）サービスのインターフェース。
/// RFC 6238準拠のTOTPの生成と検証、およびバックアップコードの管理を提供します。
/// </summary>
/// <remarks>
/// セキュリティ考慮事項:
/// <list type="bullet">
///   <item>シークレットキーは暗号化して保存し、平文でログに出力しないでください</item>
///   <item>TOTPコードは30秒間有効で、前後1ステップ（計90秒）の時間ずれを許容します</item>
///   <item>バックアップコードは一度使用したら無効化してください</item>
///   <item>Google Authenticator、Microsoft Authenticator等の認証アプリと互換性があります</item>
/// </list>
/// </remarks>
public interface ITotpService
{
    /// <summary>
    /// 新しいTOTPシークレットキーを生成します。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>Base32エンコードされたシークレットキー。</returns>
    /// <remarks>
    /// 生成されるシークレット:
    /// <list type="bullet">
    ///   <item>20バイト（160ビット）のランダムデータから生成されます</item>
    ///   <item>Base32エンコード形式で返されます</item>
    ///   <item>認証アプリで直接入力可能な形式です</item>
    /// </list>
    /// </remarks>
    Task<string> GenerateSecretAsync(CancellationToken ct = default);

    /// <summary>
    /// 認証アプリでスキャン可能なQRコード用のotpauth URIを生成します。
    /// </summary>
    /// <param name="email">ユーザーのメールアドレス（アカウント識別子として使用）。</param>
    /// <param name="secret">Base32エンコードされたシークレットキー。</param>
    /// <returns>otpauth:// 形式のURI文字列。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="email"/> または <paramref name="secret"/> が null の場合。</exception>
    /// <remarks>
    /// URI形式:
    /// <code>otpauth://totp/SkiShop:{email}?secret={secret}&amp;issuer=SkiShop&amp;digits=6&amp;period=30</code>
    /// </remarks>
    string GenerateQrCodeUri(string email, string secret);

    /// <summary>
    /// TOTPコードを検証します。
    /// </summary>
    /// <param name="secret">Base32エンコードされたシークレットキー。</param>
    /// <param name="code">検証する6桁のTOTPコード。</param>
    /// <returns>コードが有効な場合は true、無効な場合は false。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="secret"/> または <paramref name="code"/> が null の場合。</exception>
    /// <remarks>
    /// 検証動作:
    /// <list type="bullet">
    ///   <item>コードは6桁でなければなりません</item>
    ///   <item>現在の時間ステップに加え、前後1ステップ（合計3ステップ = 90秒）を許容します</item>
    ///   <item>HMAC-SHA1アルゴリズムを使用します</item>
    /// </list>
    /// </remarks>
    bool VerifyCode(string secret, string code);

    /// <summary>
    /// ワンタイム使用のバックアップコードを生成します。
    /// </summary>
    /// <param name="count">生成するバックアップコードの数。デフォルトは8。</param>
    /// <returns>生成されたバックアップコードのリスト。</returns>
    /// <remarks>
    /// 生成されるコード:
    /// <list type="bullet">
    ///   <item>各コードは8文字で、英数字（紛らわしい文字を除く）から構成されます</item>
    ///   <item>暗号学的に安全な乱数生成器を使用します</item>
    ///   <item>コードはユーザーに一度だけ表示し、安全に保管するよう案内してください</item>
    ///   <item>各コードは一度だけ使用可能です</item>
    /// </list>
    /// </remarks>
    IReadOnlyList<string> GenerateBackupCodes(int count = 8);
}
