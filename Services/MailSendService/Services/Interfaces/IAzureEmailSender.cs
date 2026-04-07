namespace MailSendService.Services.Interfaces;

/// <summary>
/// メール添付ファイルの情報を表す DTO。
/// </summary>
/// <param name="Filename">添付ファイル名。</param>
/// <param name="ContentType">MIME コンテンツタイプ（例: application/pdf）。</param>
/// <param name="Content">添付ファイルのバイナリデータ。</param>
/// <param name="ContentId">インライン添付用のコンテンツ ID（省略可）。</param>
public record EmailAttachmentInfo(string Filename, string ContentType, BinaryData Content, string? ContentId = null);

/// <summary>
/// Azure Communication Services を使用してメールを送信するサービスインターフェース。
/// </summary>
public interface IAzureEmailSender
{
    /// <summary>
    /// 指定された宛先にメールを送信する。
    /// </summary>
    /// <param name="recipientEmail">受信者のメールアドレス。</param>
    /// <param name="recipientName">受信者の表示名。</param>
    /// <param name="subject">メールの件名。</param>
    /// <param name="htmlBody">HTML 形式のメール本文。</param>
    /// <param name="plainTextBody">プレーンテキスト形式のメール本文（省略可）。</param>
    /// <param name="attachments">添付ファイルのリスト（省略可）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>送信操作のメッセージ ID。</returns>
    Task<string> SendAsync(
        string recipientEmail, string recipientName,
        string subject, string htmlBody, string? plainTextBody = null,
        IReadOnlyList<EmailAttachmentInfo>? attachments = null,
        CancellationToken ct = default);
}
