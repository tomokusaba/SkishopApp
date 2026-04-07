using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PointService.Models;

/// <summary>
/// ティア定義マスタ（ローカル参照用）。
/// Name: BRONZE / SILVER / GOLD / PLATINUM（設計書 §5.2 準拠）。
/// </summary>
[Table("tier_definitions")]
public class TierDefinition
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("name")]
    [Required]
    [MaxLength(20)]
    public string Name { get; set; } = string.Empty;

    [Column("point_rate")]
    [Required]
    public decimal PointRate { get; set; }

    [Column("min_annual_points")]
    [Required]
    public int MinAnnualPoints { get; set; }

    [Column("sort_order")]
    [Required]
    public int SortOrder { get; set; }

    [Column("benefits")]
    [MaxLength(2000)]
    public string? Benefits { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = default;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = default;
}
