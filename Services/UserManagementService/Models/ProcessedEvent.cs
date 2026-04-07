using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserManagementService.Models;

/// <summary>
/// Kafka イベントのべき等性保証用エンティティ。
/// 処理済みイベント ID を記録し、重複処理を防止する。
/// </summary>
[Table("processed_events")]
public class ProcessedEvent
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("event_id")]
    [Required]
    [MaxLength(36)]
    public string EventId { get; set; } = string.Empty;

    [Column("event_type")]
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    [Column("processed_at")]
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
