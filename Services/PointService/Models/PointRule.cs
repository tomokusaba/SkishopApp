using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PointService.Models;

/// <summary>ポイント付与ルール。</summary>
[Table("point_rules")]
public class PointRule
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("name")]
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    [Column("point_rate")]
    [Required]
    public decimal PointRate { get; set; }

    [Column("minimum_amount")]
    [Required]
    public decimal MinimumAmount { get; set; }

    [Column("is_active")]
    [Required]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = default;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = default;
}
