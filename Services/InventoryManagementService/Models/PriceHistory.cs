using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementService.Models;

/// <summary>
/// 価格変更履歴エンティティ。price_histories テーブルにマッピングされる。
/// 価格の変更を時系列で記録する監査証跡として機能し、変更理由・変更者を追跡できる。
/// </summary>
[Table("price_histories")]
public class PriceHistory
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("product_id")]
    [Required]
    [MaxLength(36)]
    public string ProductId { get; set; } = string.Empty;

    /// <summary>変更後の価格。</summary>
    [Column("price")]
    public decimal Price { get; set; }

    /// <summary>価格種別。REGULAR_PRICE（通常価格）または SALE_PRICE（セール価格）。</summary>
    [Column("price_type")]
    [Required]
    [MaxLength(50)]
    public string PriceType { get; set; } = string.Empty;

    /// <summary>価格変更の適用開始日時。</summary>
    [Column("effective_date")]
    public DateTimeOffset EffectiveDate { get; set; }

    /// <summary>価格変更の理由（例: "季節セール"、"仕入れ価格改定"）。</summary>
    [Column("reason")]
    [MaxLength(500)]
    public string? Reason { get; set; }

    /// <summary>通貨コード（ISO 4217）。デフォルトは JPY。</summary>
    [Column("currency_code")]
    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "JPY";

    /// <summary>価格を変更した管理者のユーザー ID。</summary>
    [Column("changed_by")]
    [MaxLength(255)]
    public string? ChangedBy { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>楽観的ロック用のタイムスタンプ。</summary>
    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    /// <summary>対象商品へのナビゲーション。</summary>
    public Product Product { get; set; } = null!;
}
