using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PointService.Models;

/// <summary>
/// ポイント取引履歴。
/// Type: EARN / REDEEM / EXPIRE / ADJUST / RESERVE / RELEASE / REFUND / CANCEL
/// </summary>
[Table("point_transactions")]
public class PointTransaction
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

    [Column("type")]
    [Required]
    [MaxLength(20)]
    public string Type { get; set; } = string.Empty;

    [Column("points")]
    [Required]
    public int Points { get; set; }

    [Column("balance_after")]
    [Required]
    public int BalanceAfter { get; set; }

    [Column("reference_id")]
    [MaxLength(36)]
    public string? ReferenceId { get; set; }

    [Column("reference_type")]
    [MaxLength(50)]
    public string? ReferenceType { get; set; }

    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = default;

    [ForeignKey(nameof(AccountId))]
    public PointAccount? Account { get; set; }
}
