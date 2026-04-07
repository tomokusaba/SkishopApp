using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Requests;

/// <summary>
/// パスワード変更リクエスト DTO。
/// ログイン済みユーザーが自分のパスワードを変更する際に使用する。
/// PUT /auth/password エンドポイントで使用される（認証必須）。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>CurrentPassword: 必須。現在のパスワードを正しく入力する必要がある</description></item>
///   <item><description>NewPassword: 必須、8〜100文字、大文字・小文字・数字・特殊文字を各1文字以上含む</description></item>
///   <item><description>NewPassword は CurrentPassword と異なる値である必要がある</description></item>
/// </list>
/// <para>セキュリティ考慮事項:</para>
/// <list type="bullet">
///   <item><description>パスワード変更後、既存のリフレッシュトークンは全て無効化される</description></item>
///   <item><description>PasswordChangedEvent イベントが発行され、通知メールが送信される</description></item>
/// </list>
/// </remarks>
/// <param name="CurrentPassword">現在のパスワード。本人確認に使用。ログ出力は禁止。</param>
/// <param name="NewPassword">新しいパスワード。パスワードポリシーに従う必要がある。ログ出力は禁止。</param>
public record PasswordChangeRequest(
    [Required(ErrorMessage = "現在のパスワードは必須です")]
    string CurrentPassword,

    [Required(ErrorMessage = "新しいパスワードは必須です")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "パスワードは8〜100文字で入力してください")]
    string NewPassword);
