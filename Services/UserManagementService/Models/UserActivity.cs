using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserManagementService.Models;

/// <summary>
/// ユーザーアクティビティ履歴エンティティ。プロフィル更新・ステータス変更・管理者操作等の操作履歴を保持する。
/// <c>details</c> カラムは JSONB 型で任意の構造化データを格納する。
/// </summary>
[Table("user_activities")]
public class UserActivity
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    [Column("activity_type")]
    [Required]
    [MaxLength(50)]
    public string ActivityType { get; set; } = string.Empty;

    [Column("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [Column("details", TypeName = "jsonb")]
    public string? Details { get; set; }

    [Column("ip_address")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [Column("device_info")]
    [MaxLength(500)]
    public string? DeviceInfo { get; set; }

    public User User { get; set; } = null!;
}
