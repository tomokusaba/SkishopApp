using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementService.Models;

/// <summary>
/// 商品カテゴリエンティティ。categories テーブルにマッピングされる。
/// ParentId による自己参照で階層構造（ツリー）を表現し、Level と Path で高速な階層クエリを可能にする。
/// </summary>
/// <remarks>
/// 楽観的ロック制御のため RowVersion（row_version）カラムを使用する。
/// </remarks>
[Table("categories")]
public class Category
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("name")]
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    /// <summary>親カテゴリの ID。ルートカテゴリの場合は null。</summary>
    [Column("parent_id")]
    [MaxLength(36)]
    public string? ParentId { get; set; }

    /// <summary>階層の深さ（ルート = 0）。</summary>
    [Column("level")]
    public int Level { get; set; }

    /// <summary>ルートからの階層パス（例: "/root-id/parent-id/self-id"）。祖先検索に使用する。</summary>
    [Column("path")]
    [MaxLength(500)]
    public string? Path { get; set; }

    [Column("active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>楽観的ロック用のタイムスタンプ。</summary>
    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    /// <summary>
    /// カテゴリを無効化する。
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// カテゴリを有効化する。
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>親カテゴリへのナビゲーション。ルートの場合は null。</summary>
    public Category? Parent { get; set; }

    /// <summary>子カテゴリ一覧。</summary>
    public ICollection<Category> Children { get; set; } = [];

    /// <summary>このカテゴリに属する商品一覧。</summary>
    public ICollection<Product> Products { get; set; } = [];

    /// <summary>このカテゴリに関連付けられたサイズガイド一覧。</summary>
    public ICollection<SizeGuide> SizeGuides { get; set; } = [];
}
