using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Requests;

/// <summary>
/// パスワードリセット確認リクエスト DTO。
/// パスワードリセットメールのリンクから呼び出され、新しいパスワードを設定する。
/// POST /auth/password/reset/confirm エンドポイントで使用される。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>Token: 必須。パスワードリセットメールに含まれるトークン</description></item>
///   <item><description>NewPassword: 必須、8〜100文字、大文字・小文字・数字・特殊文字を各1文字以上含む</description></item>
/// </list>
/// <para>使用フロー:</para>
/// <list type="number">
///   <item><description>PasswordResetRequest でメールアドレスを送信 → リセットメールが送信される</description></item>
///   <item><description>メール内のリンクに含まれる Token と新しいパスワードを本リクエストで送信</description></item>
///   <item><description>トークン検証成功後、パスワードが更新される</description></item>
/// </list>
/// <para>トークンの有効期限は通常1時間。使用後は無効化される。</para>
/// </remarks>
/// <param name="Token">パスワードリセット用トークン。リセットメールに含まれる。</param>
/// <param name="NewPassword">新しいパスワード。パスワードポリシーに従う必要がある。ログ出力は禁止。</param>
public record PasswordResetConfirmRequest(
    [Required(ErrorMessage = "トークンは必須です")]
    string Token,

    [Required(ErrorMessage = "新しいパスワードは必須です")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "パスワードは8〜100文字で入力してください")]
    string NewPassword);
