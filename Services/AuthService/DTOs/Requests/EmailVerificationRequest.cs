using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs.Requests;

/// <summary>
/// メールアドレス確認リクエスト DTO。
/// ユーザー登録後に送信される確認メール内のリンクから呼び出される。
/// POST /auth/email/verify エンドポイントで使用される。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>Token: 必須。メールに含まれる確認トークン（JWT または UUID 形式）。</description></item>
/// </list>
/// <para>トークンの有効期限は通常24時間。期限切れの場合は再送信が必要。</para>
/// </remarks>
/// <param name="Token">メールアドレス確認用トークン。登録時に生成され、確認メールに含まれる。</param>
public record EmailVerificationRequest(
    [Required(ErrorMessage = "トークンは必須です")]
    string Token);
