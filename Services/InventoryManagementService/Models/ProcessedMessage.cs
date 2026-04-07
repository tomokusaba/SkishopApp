using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementService.Models;

/// <summary>
/// Kafka コンシューマーのべき等性保証用エンティティ。processed_messages テーブルにマッピングされる。
/// 処理済みメッセージの MessageId を記録し、同一メッセージの重複処理を防止する。
/// </summary>
[Table("processed_messages")]
public class ProcessedMessage
{
    /// <summary>Kafka メッセージの一意識別子。重複チェックのキーとして使用する。</summary>
    [Key]
    [Column("message_id")]
    [MaxLength(255)]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>メッセージが消費された Kafka トピック名。</summary>
    [Column("topic")]
    [Required]
    [MaxLength(100)]
    public string Topic { get; set; } = string.Empty;

    /// <summary>メッセージが存在する Kafka パーティション番号。</summary>
    [Column("partition")]
    public int Partition { get; set; }

    /// <summary>パーティション内のメッセージオフセット。</summary>
    [Column("offset")]
    public long Offset { get; set; }

    [Column("processed_at")]
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
