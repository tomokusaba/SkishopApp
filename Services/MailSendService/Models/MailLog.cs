using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MailSendService.Models;

/// <summary>
/// メール送信ログを管理する EF Core エンティティ（テーブル: mail_logs）。
/// </summary>
/// <remarks>
/// DDD Aggregate Root として設計。ステータス遷移は専用メソッドで行い、不正な状態遷移を防止する。
/// </remarks>
[Table("mail_logs")]
public class MailLog
{
    /// <summary>メールログの一意識別子（UUID）。</summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>トリガーとなったイベントの種別（例: OrderPlaced）。</summary>
    [Column("event_type")]
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>トリガーとなったイベントの一意識別子。</summary>
    [Column("event_id")]
    [Required]
    [MaxLength(100)]
    public string EventId { get; set; } = string.Empty;

    /// <summary>分散トレーシング用の相関 ID。</summary>
    [Column("correlation_id")]
    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    /// <summary>送信先メールアドレス。</summary>
    [Column("recipient_email")]
    [Required]
    [MaxLength(255)]
    public string RecipientEmail { get; set; } = string.Empty;

    /// <summary>送信先の受信者名。</summary>
    [Column("recipient_name")]
    [MaxLength(200)]
    public string? RecipientName { get; set; }

    /// <summary>使用したメールテンプレートの名前。</summary>
    [Column("template_name")]
    [Required]
    [MaxLength(100)]
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>使用したメールテンプレートの ID。</summary>
    [Column("template_id")]
    [MaxLength(36)]
    public string? TemplateId { get; set; }

    /// <summary>受信者のユーザー ID。</summary>
    [Column("recipient_user_id")]
    [MaxLength(36)]
    public string? RecipientUserId { get; set; }

    /// <summary>メールの件名。</summary>
    [Column("subject")]
    [Required]
    [MaxLength(500)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>メール送信のステータス（<see cref="MailLogStatus"/> の定数値）。</summary>
    [Column("status")]
    [Required]
    [MaxLength(30)]
    public string Status { get; private set; } = MailLogStatus.Pending;

    /// <summary>Azure Communication Services の操作 ID。</summary>
    [Column("azure_operation_id")]
    [MaxLength(200)]
    public string? AzureOperationId { get; set; }

    /// <summary>送信失敗時のエラーメッセージ。</summary>
    [Column("error_message")]
    public string? ErrorMessage { get; private set; }

    /// <summary>送信リトライの実行回数。</summary>
    [Column("retry_count")]
    public int RetryCount { get; private set; }

    /// <summary>テンプレート変数の JSON シリアライズ文字列。リトライ時に変数を復元するために保存する。</summary>
    [Column("template_variables_json")]
    public string? TemplateVariablesJson { get; set; }

    /// <summary>メール送信完了日時。</summary>
    [Column("sent_at")]
    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>レコード作成日時（UTC）。</summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>レコード最終更新日時（UTC）。</summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>このメールに関連する添付ファイルのコレクション。</summary>
    public ICollection<MailAttachment> Attachments { get; set; } = [];

    // --- DDD ドメインメソッド（P0-7: Anemic Domain Model 対策） ---

    /// <summary>
    /// メールを送信中ステータスに遷移する。
    /// </summary>
    /// <exception cref="InvalidOperationException">現在のステータスが Pending でない場合。</exception>
    public void MarkAsSending()
    {
        if (Status != MailLogStatus.Pending && Status != MailLogStatus.Failed)
            throw new InvalidOperationException($"Cannot transition from {Status} to Sending");
        Status = MailLogStatus.Sending;
    }

    /// <summary>
    /// メールを送信完了ステータスに遷移する。
    /// </summary>
    /// <param name="azureOperationId">Azure Communication Services の操作 ID。</param>
    /// <param name="sentAt">送信完了日時。</param>
    /// <exception cref="InvalidOperationException">現在のステータスが Sending でない場合。</exception>
    public void MarkAsSent(string azureOperationId, DateTimeOffset sentAt)
    {
        if (Status != MailLogStatus.Sending)
            throw new InvalidOperationException($"Cannot transition from {Status} to Sent");
        Status = MailLogStatus.Sent;
        AzureOperationId = azureOperationId;
        SentAt = sentAt;
        ErrorMessage = null;
    }

    /// <summary>
    /// メールを送信失敗ステータスに遷移する。
    /// </summary>
    /// <param name="errorMessage">エラーメッセージ。</param>
    /// <exception cref="InvalidOperationException">現在のステータスが Sending でない場合。</exception>
    public void MarkAsFailed(string errorMessage)
    {
        if (Status != MailLogStatus.Sending)
            throw new InvalidOperationException($"Cannot transition from {Status} to Failed");
        Status = MailLogStatus.Failed;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// リトライ回数をインクリメントし、ステータスを Pending に戻す。
    /// </summary>
    /// <exception cref="InvalidOperationException">現在のステータスが Failed でない場合。</exception>
    public void PrepareForRetry()
    {
        if (Status != MailLogStatus.Failed)
            throw new InvalidOperationException($"Cannot retry from {Status} status");
        RetryCount++;
        Status = MailLogStatus.Pending;
        ErrorMessage = null;
    }

    /// <summary>
    /// Pending 状態のメールをスキップ済みに変更する（ユーザー削除時など）。
    /// </summary>
    /// <param name="reason">スキップ理由。</param>
    /// <exception cref="InvalidOperationException">現在のステータスが Pending でない場合。</exception>
    public void MarkAsSkipped(string reason)
    {
        if (Status != MailLogStatus.Pending)
            throw new InvalidOperationException($"Cannot skip mail with status {Status}");
        Status = MailLogStatus.Skipped;
        ErrorMessage = reason;
    }

    /// <summary>
    /// GDPR 準拠のために PII を匿名化する。
    /// </summary>
    /// <param name="anonymizedEmail">匿名化されたメールアドレス（例: SHA-256 ハッシュ）。</param>
    public void AnonymizePii(string anonymizedEmail)
    {
        RecipientEmail = anonymizedEmail;
        RecipientName = null;
        RecipientUserId = null;
        TemplateVariablesJson = null;
    }
}
