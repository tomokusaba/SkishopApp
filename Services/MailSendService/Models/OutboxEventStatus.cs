namespace MailSendService.Models;

/// <summary>
/// OutboxEvent のステータス定数。Outbox パターンにおけるイベント発行状態を定義する。
/// </summary>
public static class OutboxEventStatus
{
    /// <summary>未発行（発行待ち）。</summary>
    public const string Pending = "PENDING";
    /// <summary>Kafka への発行が完了。</summary>
    public const string Published = "PUBLISHED";
    /// <summary>Kafka への発行が失敗。クールダウン後にリトライ対象。</summary>
    public const string Failed = "FAILED";
}
