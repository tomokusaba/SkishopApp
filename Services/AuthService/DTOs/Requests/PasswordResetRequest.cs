using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Requests;

/// <summary>
/// パスワードリセット要求リクエスト DTO。
/// パスワードを忘れたユーザーがリセットメールの送信を要求する。
/// POST /auth/password/reset エンドポイントで使用される。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>Email: 必須、有効なメールアドレス形式</description></item>
/// </list>
/// <para>セキュリティ考慮事項:</para>
/// <list type="bullet">
///   <item><description>メールアドレスが登録されていない場合でも、成功レスポンスを返す（タイミング攻撃対策）</description></item>
///   <item><description>リセットトークンの有効期限は1時間</description></item>
///   <item><description>同一メールアドレスへのリクエストは1時間に3回までに制限される</description></item>
/// </list>
/// </remarks>
/// <param name="Email">パスワードをリセットするアカウントのメールアドレス。</param>
public record PasswordResetRequest(
    [Required(ErrorMessage = "メールアドレスは必須です")]
    [EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
    string Email);
