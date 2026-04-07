using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CouponService.Models;

[Table("user_coupons")]
public class UserCoupon
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    [Column("coupon_id")]
    [Required]
    [MaxLength(36)]
    public string CouponId { get; set; } = string.Empty;

    [Column("status")]
    [Required]
    public UserCouponStatus Status { get; set; } = UserCouponStatus.Available;

    [Column("acquired_at")]
    public DateTimeOffset AcquiredAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("used_at")]
    public DateTimeOffset? UsedAt { get; set; }

    [ForeignKey(nameof(CouponId))]
    public Coupon? Coupon { get; set; }
}
