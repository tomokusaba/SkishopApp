using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PaymentCartService.Models.Enums;

namespace PaymentCartService.Models;

[Table("payment_methods")]
public class PaymentMethod : IHasTimestamps
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("user_id")]
    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    [Column("type")]
    [Required]
    [MaxLength(30)]
    public PaymentMethodType Type { get; set; }

    [Column("provider")]
    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    [Column("account_reference")]
    [Required]
    [MaxLength(255)]
    public string AccountReference { get; set; } = string.Empty;

    [Column("is_default")]
    public bool IsDefault { get; set; }

    [Column("expiry_date")]
    public DateTime? ExpiryDate { get; set; }

    [Column("billing_address_id")]
    [MaxLength(36)]
    public string? BillingAddressId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
