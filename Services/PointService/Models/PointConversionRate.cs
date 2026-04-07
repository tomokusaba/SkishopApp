using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PointService.Models;

/// <summary>ポイント換算レート。</summary>
[Table("point_conversion_rates")]
public class PointConversionRate
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("currency_code")]
    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Column("rate_per_point")]
    [Required]
    public decimal RatePerPoint { get; set; }

    [Column("is_active")]
    [Required]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = default;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = default;
}
