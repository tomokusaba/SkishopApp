using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementService.Models;

/// <summary>
/// 商品カタログの Aggregate Root。products テーブルにマッピングされる。
/// SKU・名称・ブランド・カテゴリなどの商品マスタ情報を保持し、
/// 在庫（Inventory）・価格（Price）・画像（ProductImage）・レビュー（Review）を子エンティティとして管理する。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>楽観的ロック制御のため RowVersion（row_version）カラムを使用する。</item>
/// <item>Attributes カラムは PostgreSQL の jsonb 型で、商品固有の可変属性を格納する。</item>
/// <item>Tags は PostgreSQL の配列型で、検索用タグを格納する。</item>
/// </list>
/// </remarks>
[Table("products")]
public class Product
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>在庫管理用の一意な商品コード（Stock Keeping Unit）。</summary>
    [Column("sku")]
    [Required]
    [MaxLength(100)]
    public string Sku { get; set; } = string.Empty;

    [Column("name")]
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("brand")]
    [MaxLength(100)]
    public string? Brand { get; set; }

    /// <summary>商品固有の可変属性を JSON 形式で保持する（PostgreSQL jsonb 型）。</summary>
    [Column("attributes", TypeName = "jsonb")]
    public string? Attributes { get; set; }

    /// <summary>検索・フィルタリング用の商品タグ（PostgreSQL 配列型）。</summary>
    [Column("tags")]
    public string[]? Tags { get; set; }

    [Column("category_id")]
    [MaxLength(36)]
    public string? CategoryId { get; set; }

    /// <summary>商品重量（kg 単位）。配送料金の算出に使用する。</summary>
    [Column("weight")]
    public decimal? Weight { get; set; }

    [Column("active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>楽観的ロック用のタイムスタンプ。EF Core が自動管理する。</summary>
    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    /// <summary>
    /// 商品情報を更新する。
    /// </summary>
    /// <param name="name">商品名</param>
    /// <param name="description">商品説明</param>
    /// <param name="brand">ブランド名</param>
    /// <param name="weight">重量（kg）</param>
    /// <param name="attributes">JSONB 形式の属性データ</param>
    /// <param name="tags">タグ配列</param>
    public void Update(string name, string? description, string? brand, decimal? weight, string? attributes, string[]? tags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Description = description;
        Brand = brand;
        Weight = weight;
        Attributes = attributes;
        Tags = tags;
    }

    /// <summary>
    /// 商品を無効化（論理削除）する。
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// 商品を有効化する。
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>所属カテゴリへのナビゲーション。</summary>
    public Category? Category { get; set; }

    /// <summary>ロケーション別在庫一覧。</summary>
    public ICollection<Inventory> Inventories { get; set; } = [];

    /// <summary>価格設定一覧（通常価格・セール価格）。</summary>
    public ICollection<Price> Prices { get; set; } = [];

    /// <summary>商品画像一覧（Azure Blob Storage URL）。</summary>
    public ICollection<ProductImage> Images { get; set; } = [];

    /// <summary>カスタマーレビュー一覧。</summary>
    public ICollection<Review> Reviews { get; set; } = [];
}
