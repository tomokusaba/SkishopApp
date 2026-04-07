using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementService.Models;

/// <summary>
/// 仕入先マスタエンティティ。suppliers テーブルにマッピングされる。
/// 仕入先の連絡先情報・有効状態を管理し、ProductSupplier を通じて商品と多対多の関係を持つ。
/// </summary>
[Table("suppliers")]
public class Supplier
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("name")]
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>仕入先の担当者名。</summary>
    [Column("contact_person")]
    [MaxLength(255)]
    public string? ContactPerson { get; set; }

    [Column("email")]
    [MaxLength(255)]
    public string? Email { get; set; }

    [Column("phone")]
    [MaxLength(50)]
    public string? Phone { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("created_by")]
    [MaxLength(255)]
    public string? CreatedBy { get; set; }

    [Column("updated_by")]
    [MaxLength(255)]
    public string? UpdatedBy { get; set; }

    /// <summary>楽観的ロック用のタイムスタンプ。</summary>
    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    /// <summary>この仕入先が取り扱う商品との関連一覧。</summary>
    public ICollection<ProductSupplier> ProductSuppliers { get; set; } = [];
}
