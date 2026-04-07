using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CouponService.Models;

[Table("coupon_usages")]
public class CouponUsage
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("coupon_id")]
    [Required]
    [MaxLength(36)]
    public string CouponId { get; set; } = string.Empty;

    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    [Column("order_id")]
    [Required]
    [MaxLength(36)]
    public string OrderId { get; set; } = string.Empty;

    [Column("discount_amount")]
    [Required]
    public decimal DiscountAmount { get; set; }

    [Column("used_at")]
    public DateTimeOffset UsedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("is_released")]
    public bool IsReleased { get; set; }

    [Column("released_at")]
    public DateTimeOffset? ReleasedAt { get; set; }

    [ForeignKey(nameof(CouponId))]
    public Coupon? Coupon { get; set; }
}
