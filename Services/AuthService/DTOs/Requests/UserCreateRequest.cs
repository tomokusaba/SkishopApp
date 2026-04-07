using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Requests;

/// <summary>
/// ユーザー新規登録リクエスト DTO。
/// 新しいユーザーアカウントを作成する。
/// POST /auth/register エンドポイントで使用される。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>Email: 必須、有効なメールアドレス形式、最大255文字、一意制約あり</description></item>
///   <item><description>Username: 必須、3〜100文字、英数字・ハイフン・アンダースコアのみ、一意制約あり</description></item>
///   <item><description>Password: 必須、8〜100文字、大文字・小文字・数字・特殊文字を各1文字以上含む</description></item>
///   <item><description>FirstName: 任意、最大100文字</description></item>
///   <item><description>LastName: 任意、最大100文字</description></item>
/// </list>
/// <para>登録後のフロー:</para>
/// <list type="bullet">
///   <item><description>UserRegisteredEvent イベントが発行される</description></item>
///   <item><description>メールアドレス確認メールが送信される</description></item>
///   <item><description>デフォルトロール "USER" が割り当てられる</description></item>
/// </list>
/// </remarks>
/// <param name="Email">ユーザーのメールアドレス。ログイン時の識別子として使用。</param>
/// <param name="Username">一意のユーザー名。公開プロフィールに表示される。</param>
/// <param name="Password">アカウントのパスワード。ハッシュ化して保存される。ログ出力は禁止。</param>
/// <param name="FirstName">ユーザーの名（任意）。</param>
/// <param name="LastName">ユーザーの姓（任意）。</param>
public record UserCreateRequest(
    [Required(ErrorMessage = "メールアドレスは必須です")]
    [EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
    [StringLength(255)]
    string Email,

    [Required(ErrorMessage = "ユーザー名は必須です")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "ユーザー名は3〜100文字で入力してください")]
    string Username,

    [Required(ErrorMessage = "パスワードは必須です")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "パスワードは8〜100文字で入力してください")]
    string Password,

    [StringLength(100)]
    string? FirstName,

    [StringLength(100)]
    string? LastName);
