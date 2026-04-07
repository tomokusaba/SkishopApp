namespace MailSendService.DTOs;

/// <summary>
/// イベントペイロードから解決されたメール送信データ。
/// </summary>
/// <param name="TemplateName">使用するテンプレート名。</param>
/// <param name="RecipientEmail">送信先メールアドレス。</param>
/// <param name="RecipientName">送信先の表示名（省略可）。</param>
/// <param name="RecipientUserId">受信者のユーザー ID（省略可）。</param>
/// <param name="Variables">テンプレート変数の辞書。</param>
public record ResolvedEventData(
    string TemplateName,
    string RecipientEmail,
    string? RecipientName,
    string? RecipientUserId,
    Dictionary<string, object> Variables);
