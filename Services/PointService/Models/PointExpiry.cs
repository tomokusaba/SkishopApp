using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PointService.Models;

/// <summary>
/// ポイント有効期限管理。Status: ACTIVE / EXPIRED / CONSUMED
/// </summary>
[Table("point_expiries")]
public class PointExpiry
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("account_id")]
    [Required]
    [MaxLength(36)]
    public string AccountId { get; set; } = string.Empty;

    [Column("user_id")]
    [Required]
    public Guid UserId { get; set; }

    [Column("points")]
    [Required]
    public int Points { get; set; }

    [Column("expires_at")]
    [Required]
    public DateTime ExpiresAt { get; set; }

    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = ExpiryStatuses.Active;

    [Column("source_transaction_id")]
    [MaxLength(36)]
    public string? SourceTransactionId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = default;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = default;

    [ForeignKey(nameof(AccountId))]
    public PointAccount? Account { get; set; }

    [ForeignKey(nameof(SourceTransactionId))]
    public PointTransaction? SourceTransaction { get; set; }
}
