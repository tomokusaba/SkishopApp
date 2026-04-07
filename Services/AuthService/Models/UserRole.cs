using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Models;

/// <summary>
/// ユーザーロール関連エンティティ。ユーザーとロールの多対多関係を表す中間テーブル。
/// RBAC（ロールベースアクセス制御）の実装に使用される。
/// </summary>
/// <remarks>
/// <para>
/// 1 人のユーザーは複数のロールを持つことができ、1 つのロールは複数のユーザーに割り当てられる。
/// </para>
/// <para>
/// 注意: この UserRole は追加の権限グループを管理する。ユーザーのプライマリロールは
/// <see cref="User.Role"/>（<see cref="Enums.UserRoleType"/>）で管理される。
/// </para>
/// </remarks>
[Table("user_roles")]
public class UserRole : IHasTimestamps
{
    /// <summary>
    /// ユーザーロール関連の一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// ロールを割り当てられたユーザーの ID。<see cref="User"/> への外部キー。
    /// </summary>
    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 割り当てられたロールの ID。<see cref="Role"/> への外部キー。
    /// </summary>
    [Column("role_id")]
    [Required]
    [MaxLength(36)]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>
    /// ロール割り当ての作成日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// ロール割り当ての最終更新日時（UTC）。
    /// </summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// ロールが割り当てられたユーザーへのナビゲーションプロパティ。
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// 割り当てられたロールへのナビゲーションプロパティ。
    /// </summary>
    public Role Role { get; set; } = null!;
}
