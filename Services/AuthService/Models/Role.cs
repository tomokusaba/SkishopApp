using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Models;

/// <summary>
/// ロールエンティティ。RBAC（ロールベースアクセス制御）における権限グループを定義する。
/// ユーザーは複数のロールを持つことができる（多対多の関係）。
/// </summary>
/// <remarks>
/// <para>
/// ロールは <see cref="UserRole"/> 中間テーブルを介してユーザーと関連付けられる。
/// </para>
/// <para>
/// 注意: <see cref="Enums.UserRoleType"/> はユーザーのプライマリロールを表すのに対し、
/// この Role エンティティは追加の権限グループを管理する。
/// </para>
/// </remarks>
[Table("roles")]
public class Role : IHasTimestamps
{
    /// <summary>
    /// ロールの一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// ロールの識別名。システム内で一意である必要がある（例: "ProductManager", "OrderProcessor"）。
    /// </summary>
    [Column("name")]
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// ロールの説明。管理画面での表示に使用。
    /// </summary>
    [Column("description")]
    [MaxLength(200)]
    public string? Description { get; set; }

    /// <summary>
    /// ロールの作成日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// ロールの最終更新日時（UTC）。
    /// </summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// このロールに属するユーザーとの関連（多対多）を表す中間エンティティのコレクション。
    /// </summary>
    public ICollection<UserRole> UserRoles { get; set; } = [];
}
