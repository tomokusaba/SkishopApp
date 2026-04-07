using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Requests;

/// <summary>
/// ユーザーログインリクエスト DTO。
/// メールアドレスとパスワードによる認証を行い、JWT アクセストークンを取得する。
/// POST /auth/login エンドポイントで使用される。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>Email: 必須、有効なメールアドレス形式、最大255文字</description></item>
///   <item><description>Password: 必須、8〜100文字</description></item>
/// </list>
/// <para>セキュリティ考慮事項:</para>
/// <list type="bullet">
///   <item><description>5回連続でログイン失敗するとアカウントが一時ロックされる</description></item>
///   <item><description>MFA が有効なユーザーの場合、MfaRequiredException がスローされる</description></item>
///   <item><description>レート制限が適用される（1分間に10回まで）</description></item>
/// </list>
/// </remarks>
/// <param name="Email">ログインに使用するメールアドレス。</param>
/// <param name="Password">ユーザーのパスワード。ログ出力は禁止。</param>
public record LoginRequest(
    [Required(ErrorMessage = "メールアドレスは必須です")]
    [EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
    [StringLength(255)]
    string Email,

    [Required(ErrorMessage = "パスワードは必須です")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "パスワードは8〜100文字で入力してください")]
    string Password);
