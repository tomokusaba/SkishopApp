namespace MailSendService.DTOs.Responses;

/// <summary>
/// メール送信ログの API レスポンス DTO。
/// </summary>
/// <param name="Id">メールログの一意識別子。</param>
/// <param name="EventType">トリガーとなったイベントの種別。</param>
/// <param name="RecipientEmail">送信先メールアドレス。</param>
/// <param name="RecipientName">送信先の受信者名。</param>
/// <param name="TemplateName">使用したテンプレート名。</param>
/// <param name="Subject">メールの件名。</param>
/// <param name="Status">送信ステータス。</param>
/// <param name="RetryCount">リトライ実行回数。</param>
/// <param name="ErrorMessage">エラーメッセージ（失敗時のみ）。</param>
/// <param name="SentAt">送信完了日時。</param>
/// <param name="CreatedAt">レコード作成日時。</param>
public record MailLogResponse(
    string Id,
    string EventType,
    string RecipientEmail,
    string? RecipientName,
    string TemplateName,
    string Subject,
    string Status,
    int RetryCount,
    string? ErrorMessage,
    DateTimeOffset? SentAt,
    DateTimeOffset CreatedAt);
