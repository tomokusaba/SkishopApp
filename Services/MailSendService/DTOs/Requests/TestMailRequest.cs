using System.ComponentModel.DataAnnotations;

namespace MailSendService.DTOs.Requests;

/// <summary>
/// テストメール送信のリクエスト DTO。
/// </summary>
/// <param name="RecipientEmail">送信先メールアドレス。</param>
/// <param name="TemplateName">使用するテンプレート名。</param>
/// <param name="Variables">テンプレートに埋め込む変数（任意）。</param>
public record TestMailRequest(
    [Required, EmailAddress, StringLength(255)]
    string RecipientEmail,
    [Required, StringLength(100)]
    string TemplateName,
    Dictionary<string, object>? Variables);
