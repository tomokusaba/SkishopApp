using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementService.Models;

/// <summary>
/// カテゴリ別サイズガイドエンティティ。size_guides テーブルにマッピングされる。
/// カテゴリに紐づくサイズチャートを PostgreSQL の jsonb 型で柔軟に格納する。
/// </summary>
/// <remarks>
/// SizeChart カラムはスキーマレスの JSON 構造を許容し、カテゴリごとに異なるサイズ体系（衣類・靴・スキー板等）を表現できる。
/// </remarks>
[Table("size_guides")]
public class SizeGuide
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("category_id")]
    [Required]
    [MaxLength(36)]
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>サイズチャートの JSON データ（PostgreSQL jsonb 型）。</summary>
    [Column("size_chart", TypeName = "jsonb")]
    [Required]
    public string SizeChart { get; set; } = string.Empty;

    /// <summary>ガイド種別（例: "CLOTHING", "SHOES", "SKI_BOARD" 等）。</summary>
    [Column("guide_type")]
    [Required]
    [MaxLength(50)]
    public string GuideType { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>楽観的ロック用のタイムスタンプ。</summary>
    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    /// <summary>所属カテゴリへのナビゲーション。</summary>
    public Category Category { get; set; } = null!;
}
