using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Requests;

/// <summary>
/// 多要素認証（MFA）検証リクエスト DTO。
/// TOTP（Time-based One-Time Password）による二段階認証を完了する。
/// POST /auth/mfa/verify エンドポイントで使用される。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>Code: 必須、6桁の数字（TOTP コード）</description></item>
///   <item><description>SessionToken: 必須、ログイン時に発行された一時セッショントークン</description></item>
/// </list>
/// <para>使用フロー:</para>
/// <list type="number">
///   <item><description>LoginRequest でログイン → MfaRequiredException がスローされ SessionToken を取得</description></item>
///   <item><description>認証アプリで生成された6桁コードと SessionToken を本リクエストで送信</description></item>
///   <item><description>検証成功時、正式な JWT アクセストークンが発行される</description></item>
/// </list>
/// </remarks>
/// <param name="Code">認証アプリ（Google Authenticator 等）で生成された6桁の TOTP コード。</param>
/// <param name="SessionToken">ログイン時に発行された MFA 認証用の一時セッショントークン。有効期限5分。</param>
public record MfaVerificationRequest(
    [Required(ErrorMessage = "認証コードは必須です")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "認証コードは6桁で入力してください")]
    string Code,

    [Required(ErrorMessage = "セッショントークンは必須です")]
    string SessionToken);
