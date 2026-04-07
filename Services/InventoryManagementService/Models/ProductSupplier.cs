using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementService.Models;

/// <summary>
/// 商品−仕入先の多対多中間エンティティ。product_suppliers テーブルにマッピングされる。
/// 仕入先固有の商品コード・仕入価格・リードタイムなどの取引条件を保持する。
/// </summary>
/// <remarks>
/// 複合主キー（ProductId + SupplierId）は DbContext の OnModelCreating で設定する。
/// </remarks>
[Table("product_suppliers")]
public class ProductSupplier
{
    [Column("product_id")]
    [MaxLength(36)]
    public string ProductId { get; set; } = string.Empty;

    [Column("supplier_id")]
    [MaxLength(36)]
    public string SupplierId { get; set; } = string.Empty;

    /// <summary>仕入先での商品コード。</summary>
    [Column("supplier_product_code")]
    [MaxLength(255)]
    public string? SupplierProductCode { get; set; }

    /// <summary>仕入価格。</summary>
    [Column("supplier_price")]
    public decimal? SupplierPrice { get; set; }

    /// <summary>発注から納品までのリードタイム（日数）。</summary>
    [Column("lead_time_days")]
    public int? LeadTimeDays { get; set; }

    /// <summary>この仕入先への最終発注日。</summary>
    [Column("last_order_date")]
    public DateTimeOffset? LastOrderDate { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>楽観的ロック用のタイムスタンプ。</summary>
    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    /// <summary>商品へのナビゲーション。</summary>
    public Product Product { get; set; } = null!;

    /// <summary>仕入先へのナビゲーション。</summary>
    public Supplier Supplier { get; set; } = null!;
}
