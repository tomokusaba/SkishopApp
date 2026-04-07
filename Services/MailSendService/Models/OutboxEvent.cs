using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MailSendService.Models;

/// <summary>
/// Outbox パターンで使用するイベントを管理する EF Core エンティティ（テーブル: outbox_events）。
/// </summary>
/// <remarks>
/// <para>DB トランザクションとイベント発行の整合性を保証するために使用される。</para>
/// <para>DDD Aggregate Root として設計。ステータス遷移は専用メソッドで行う。</para>
/// </remarks>
[Table("outbox_events")]
public class OutboxEvent
{
    /// <summary>イベントの一意識別子（UUID）。</summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>イベントの種別名。</summary>
    [Column("event_type")]
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>イベントのペイロード（JSON 形式）。</summary>
    [Column("payload")]
    [Required]
    public string Payload { get; set; } = "{}";

    /// <summary>イベントの発行ステータス（PENDING / PUBLISHED / FAILED）。</summary>
    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; private set; } = OutboxEventStatus.Pending;

    /// <summary>レコード作成日時（UTC）。</summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>レコード最終更新日時（UTC）。P1-13: リトライバックオフで使用。</summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>イベントが発行された日時。</summary>
    [Column("published_at")]
    public DateTimeOffset? PublishedAt { get; private set; }

    // --- DDD ドメインメソッド（P0-8: Anemic Domain Model 対策） ---

    /// <summary>
    /// イベントを発行完了ステータスに遷移する。
    /// </summary>
    /// <param name="publishedAt">発行完了日時。</param>
    /// <exception cref="InvalidOperationException">現在のステータスが Pending でない場合。</exception>
    public void MarkAsPublished(DateTimeOffset publishedAt)
    {
        if (Status != OutboxEventStatus.Pending)
            throw new InvalidOperationException($"Cannot transition from {Status} to Published");
        Status = OutboxEventStatus.Published;
        PublishedAt = publishedAt;
    }

    /// <summary>
    /// イベントを発行失敗ステータスに遷移する。
    /// </summary>
    /// <exception cref="InvalidOperationException">現在のステータスが Pending でない場合。</exception>
    public void MarkAsFailed()
    {
        if (Status != OutboxEventStatus.Pending)
            throw new InvalidOperationException($"Cannot transition from {Status} to Failed");
        Status = OutboxEventStatus.Failed;
    }

    /// <summary>
    /// 失敗したイベントを Pending に戻してリトライ可能にする。
    /// </summary>
    /// <exception cref="InvalidOperationException">現在のステータスが Failed でない場合。</exception>
    public void ResetToPending()
    {
        if (Status != OutboxEventStatus.Failed)
            throw new InvalidOperationException($"Cannot reset from {Status} to Pending");
        Status = OutboxEventStatus.Pending;
        PublishedAt = null;
    }
}
