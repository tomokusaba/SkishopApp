using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementService.Models;

/// <summary>
/// 商品画像エンティティ。product_images テーブルにマッピングされる。
/// Product Aggregate の子エンティティとして、Azure Blob Storage 上の画像 URL を管理する。
/// </summary>
/// <remarks>
/// SortOrder で表示順序を制御し、Type で画像の用途（MAIN / THUMBNAIL / GALLERY 等）を区別する。
/// </remarks>
[Table("product_images")]
public class ProductImage
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("product_id")]
    [Required]
    [MaxLength(36)]
    public string ProductId { get; set; } = string.Empty;

    /// <summary>画像の Azure Blob Storage URL。</summary>
    [Column("url")]
    [Required]
    [MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    /// <summary>サムネイル用の URL。リスト表示等で使用する。</summary>
    [Column("thumbnail_url")]
    [MaxLength(500)]
    public string? ThumbnailUrl { get; set; }

    /// <summary>画像種別（MAIN / THUMBNAIL / GALLERY 等）。</summary>
    [Column("type")]
    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = "MAIN";

    /// <summary>表示順序（昇順）。</summary>
    [Column("sort_order")]
    public int SortOrder { get; set; }

    /// <summary>アクセシビリティ用の代替テキスト。</summary>
    [Column("alt_text")]
    [MaxLength(200)]
    public string? AltText { get; set; }

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

    /// <summary>親商品へのナビゲーション。</summary>
    public Product Product { get; set; } = null!;
}
