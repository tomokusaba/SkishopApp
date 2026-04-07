using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CouponService.Models;

[Table("promotions")]
public class Promotion
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

    [Column("type")]
    [MaxLength(50)]
    public string? Type { get; set; }

    [Column("conditions", TypeName = "jsonb")]
    public string? Conditions { get; set; }

    [Column("application_rules", TypeName = "jsonb")]
    public string? ApplicationRules { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("start_date")]
    [Required]
    public DateTimeOffset StartDate { get; set; }

    [Column("end_date")]
    [Required]
    public DateTimeOffset EndDate { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
