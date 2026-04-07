using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserManagementService.Models;

/// <summary>
/// Outbox パターン用イベントエンティティ。DB トランザクションとイベント発行の整合性を保証する。
/// <see cref="BackgroundServices.OutboxPublisher"/> が定期的に PENDING イベントを Kafka へ発行する。
/// </summary>
[Table("outbox_events")]
public class OutboxEvent
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("event_type")]
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    [Column("aggregate_id")]
    [Required]
    [MaxLength(36)]
    public string AggregateId { get; set; } = string.Empty;

    [Column("payload", TypeName = "jsonb")]
    [Required]
    public string Payload { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }

    [Column("retry_count")]
    public int RetryCount { get; set; }

    [Column("max_retries")]
    public int MaxRetries { get; set; } = 5;

    [Column("last_error")]
    [MaxLength(1000)]
    public string? LastError { get; set; }

    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = OutboxEventStatus.Pending;
}

/// <summary>
/// Outbox イベントのステータス定数。
/// </summary>
public static class OutboxEventStatus
{
    public const string Pending = "PENDING";
    public const string Published = "PUBLISHED";
    public const string Failed = "FAILED";
}
