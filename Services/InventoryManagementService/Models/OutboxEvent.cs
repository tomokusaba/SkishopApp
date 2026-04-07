using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementService.Models;

/// <summary>
/// トランザクショナル Outbox パターン用イベントエンティティ。outbox_events テーブルにマッピングされる。
/// DB トランザクションとイベント発行の整合性を保証するために、イベントを DB に一時保存し、
/// BackgroundService（OutboxPublisher）が非同期で Kafka に発行する。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Status は PENDING → PUBLISHED（正常発行）または FAILED（リトライ上限超過）に遷移する。</item>
/// <item>RetryCount が MaxRetries に達すると発行停止し、LastError に最後のエラー情報を記録する。</item>
/// </list>
/// </remarks>
[Table("outbox_events")]
public class OutboxEvent
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>イベントの型名（例: "ProductCreatedEvent"）。</summary>
    [Column("event_type")]
    [Required]
    [MaxLength(255)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>イベント発生元の集約ルート ID。</summary>
    [Column("aggregate_id")]
    [Required]
    [MaxLength(36)]
    public string AggregateId { get; set; } = string.Empty;

    /// <summary>イベント発生元の集約型名（例: "Product", "Inventory"）。</summary>
    [Column("aggregate_type")]
    [Required]
    [MaxLength(100)]
    public string AggregateType { get; set; } = string.Empty;

    /// <summary>発行先の Kafka トピック名。</summary>
    [Column("topic")]
    [Required]
    [MaxLength(255)]
    public string Topic { get; set; } = string.Empty;

    /// <summary>イベントデータの JSON シリアライズ済みペイロード。</summary>
    [Column("payload")]
    [Required]
    public string Payload { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Kafka への発行完了日時。未発行の場合は null。</summary>
    [Column("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>発行リトライ回数。</summary>
    [Column("retry_count")]
    public int RetryCount { get; set; }

    /// <summary>最大リトライ回数。この値を超えると FAILED に遷移する。</summary>
    [Column("max_retries")]
    public int MaxRetries { get; set; } = 5;

    /// <summary>最後の発行エラーメッセージ。</summary>
    [Column("last_error")]
    [MaxLength(2000)]
    public string? LastError { get; set; }

    /// <summary>イベント発行ステータス。PENDING / PROCESSING / PUBLISHED / FAILED / DEAD_LETTER のいずれか。</summary>
    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "PENDING";

    /// <summary>リクエストの相関 ID。分散トレーシングに使用。</summary>
    [Column("correlation_id")]
    [MaxLength(36)]
    public string? CorrelationId { get; set; }
}
