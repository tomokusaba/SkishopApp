using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CouponService.Models;

[Table("coupons")]
public class Coupon
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("code")]
    [Required]
    [MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [Column("coupon_type_id")]
    [MaxLength(36)]
    public string? CouponTypeId { get; set; }

    [Column("campaign_id")]
    [MaxLength(36)]
    public string? CampaignId { get; set; }

    [Column("discount_type")]
    [Required]
    public DiscountType DiscountType { get; set; }

    [Column("discount_value")]
    [Required]
    public decimal DiscountValue { get; set; }

    [Column("max_discount_amount")]
    public decimal? MaxDiscountAmount { get; set; }

    [Column("min_order_amount")]
    public decimal MinOrderAmount { get; set; }

    [Column("max_usage_count")]
    public int MaxUsageCount { get; set; } = 1;

    [Column("current_usage_count")]
    public int CurrentUsageCount { get; set; }

    [Column("max_usage_per_user")]
    public int MaxUsagePerUser { get; set; } = 1;

    [Column("valid_from")]
    [Required]
    public DateTimeOffset ValidFrom { get; set; }

    [Column("valid_until")]
    [Required]
    public DateTimeOffset ValidUntil { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    [ForeignKey(nameof(CouponTypeId))]
    public CouponType? CouponType { get; set; }

    [ForeignKey(nameof(CampaignId))]
    public Campaign? Campaign { get; set; }

    public ICollection<CouponRestriction> Restrictions { get; set; } = [];
    public ICollection<UserCoupon> UserCoupons { get; set; } = [];
    public ICollection<CouponUsage> Usages { get; set; } = [];
}
