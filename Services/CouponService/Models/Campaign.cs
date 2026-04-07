using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CouponService.Models;

[Table("campaigns")]
public class Campaign
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("name")]
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(2000)]
    public string? Description { get; set; }

    [Column("status")]
    [Required]
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

    [Column("start_date")]
    [Required]
    public DateTimeOffset StartDate { get; set; }

    [Column("end_date")]
    [Required]
    public DateTimeOffset EndDate { get; set; }

    [Column("max_coupons")]
    public int MaxCoupons { get; set; }

    [Column("issued_count")]
    public int IssuedCount { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    public ICollection<Coupon> Coupons { get; set; } = [];
}
