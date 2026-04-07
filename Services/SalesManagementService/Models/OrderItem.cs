using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SalesManagementService.Models;

[Table("order_items")]
public class OrderItem
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("order_id")]
    [Required]
    [MaxLength(36)]
    public string OrderId { get; set; } = string.Empty;

    [Column("product_id")]
    [Required]
    [MaxLength(100)]
    public string ProductId { get; set; } = string.Empty;

    [Column("product_name")]
    [Required]
    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [Column("sku")]
    [Required]
    [MaxLength(100)]
    public string Sku { get; set; } = string.Empty;

    [Column("unit_price")]
    [Precision(12, 2)]
    public decimal UnitPrice { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("subtotal")]
    [Precision(12, 2)]
    public decimal Subtotal { get; set; }

    [Column("product_snapshot", TypeName = "jsonb")]
    public string? ProductSnapshot { get; set; }

    [Column("applied_coupon_id")]
    [MaxLength(100)]
    public string? AppliedCouponId { get; set; }

    [Column("coupon_discount_amount")]
    [Precision(12, 2)]
    public decimal CouponDiscountAmount { get; set; }

    [Column("used_points")]
    public int UsedPoints { get; set; }

    [Column("point_discount_amount")]
    [Precision(12, 2)]
    public decimal PointDiscountAmount { get; set; }

    // ── ナビゲーションプロパティ ──
    public Order Order { get; set; } = null!;
}
