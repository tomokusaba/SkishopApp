using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PaymentCartService.Models.Enums;

namespace PaymentCartService.Models;

[Table("transactions")]
public class Transaction : IHasTimestamps
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("payment_id")]
    [Required]
    [MaxLength(36)]
    public string PaymentId { get; set; } = string.Empty;

    [Column("type")]
    [Required]
    [MaxLength(30)]
    public TransactionType Type { get; set; }

    [Column("amount", TypeName = "decimal(12,2)")]
    [Required]
    public decimal Amount { get; set; }

    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    [Column("gateway_response")]
    public string? GatewayResponse { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(PaymentId))]
    public Payment Payment { get; set; } = null!;
}
