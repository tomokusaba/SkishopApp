using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PointService.Models;

/// <summary>
/// 管理者ポイント操作の監査ログ（設計書 §19.2 H-09 対応）。
/// Action: ADD / SUBTRACT / ADJUST
/// </summary>
[Table("point_audit_logs")]
public class PointAuditLog
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("admin_user_id")]
    [Required]
    [MaxLength(36)]
    public string AdminUserId { get; set; } = string.Empty;

    [Column("target_user_id")]
    [Required]
    [MaxLength(36)]
    public string TargetUserId { get; set; } = string.Empty;

    [Column("action")]
    [Required]
    [MaxLength(20)]
    public string Action { get; set; } = string.Empty;

    [Column("points_before")]
    public int PointsBefore { get; set; }

    [Column("points_after")]
    public int PointsAfter { get; set; }

    [Column("points_changed")]
    public int PointsChanged { get; set; }

    [Column("reason")]
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [Column("ip_address")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = default;
}
