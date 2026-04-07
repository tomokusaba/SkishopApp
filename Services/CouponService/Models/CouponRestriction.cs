using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CouponService.Models;

[Table("coupon_restrictions")]
public class CouponRestriction
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("coupon_id")]
    [Required]
    [MaxLength(36)]
    public string CouponId { get; set; } = string.Empty;

    [Column("restriction_type")]
    [Required]
    [MaxLength(20)]
    public string RestrictionType { get; set; } = string.Empty;

    [Column("restriction_value")]
    [Required]
    [MaxLength(500)]
    public string RestrictionValue { get; set; } = string.Empty;

    [Column("is_exclusion")]
    public bool IsExclusion { get; set; }

    [Column("percentage_max")]
    public decimal? PercentageMax { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [ForeignKey(nameof(CouponId))]
    public Coupon? Coupon { get; set; }
}
