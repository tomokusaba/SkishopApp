using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserManagementService.Models;

/// <summary>
/// ユーザー設定エンティティ。言語・通貨・通知・表示の各種プリファレンスを保持する。
/// User と 1:1 リレーション。ユーザー登録時にデフォルト値で初期化される。
/// </summary>
[Table("user_preferences")]
public class UserPreference
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    [Column("language")]
    [MaxLength(10)]
    public string Language { get; set; } = "ja";

    [Column("currency")]
    [MaxLength(3)]
    public string Currency { get; set; } = "JPY";

    [Column("notification_preferences", TypeName = "jsonb")]
    public string? NotificationPreferences { get; set; }

    [Column("display_preferences", TypeName = "jsonb")]
    public string? DisplayPreferences { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
}
