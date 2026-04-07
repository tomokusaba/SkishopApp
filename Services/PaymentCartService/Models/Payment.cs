using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PaymentCartService.Models.Enums;

namespace PaymentCartService.Models;

[Table("payments")]
public class Payment : IHasTimestamps
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("order_id")]
    [Required]
    [MaxLength(36)]
    public string OrderId { get; set; } = string.Empty;

    [Column("customer_id")]
    [Required]
    [MaxLength(100)]
    public string CustomerId { get; set; } = string.Empty;

    [Column("stripe_checkout_session_id")]
    [MaxLength(100)]
    public string? StripeCheckoutSessionId { get; set; }

    [Column("stripe_payment_intent_id")]
    [MaxLength(100)]
    public string? StripePaymentIntentId { get; set; }

    [Column("stripe_charge_id")]
    [MaxLength(100)]
    public string? StripeChargeId { get; set; }

    [Column("amount", TypeName = "decimal(12,2)")]
    [Required]
    public decimal Amount { get; set; }

    [Column("currency_code")]
    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "JPY";

    [Column("status")]
    [Required]
    [MaxLength(20)]
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [Column("payment_method")]
    [Required]
    [MaxLength(50)]
    public string PaymentMethod { get; set; } = string.Empty;

    [Column("failure_code")]
    [MaxLength(100)]
    public string? FailureCode { get; set; }

    [Column("failure_message")]
    [MaxLength(500)]
    public string? FailureMessage { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("created_by")]
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [Column("updated_by")]
    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    private readonly List<Transaction> _transactions = [];
    public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

    public void AddTransaction(TransactionType type, decimal amount, string status, string? gatewayResponse = null)
    {
        _transactions.Add(new Transaction
        {
            PaymentId = Id,
            Type = type,
            Amount = amount,
            Status = status,
            GatewayResponse = gatewayResponse
        });
    }

    public void MarkAsCompleted(string stripePaymentIntentId, string? stripeChargeId, DateTime paidAt)
    {
        Status = PaymentStatus.Completed;
        StripePaymentIntentId = stripePaymentIntentId;
        StripeChargeId = stripeChargeId;
        PaidAt = paidAt;
    }

    public void MarkAsFailed(string failureCode, string failureMessage)
    {
        Status = PaymentStatus.Failed;
        FailureCode = failureCode;
        FailureMessage = failureMessage;
    }

    public void MarkAsRefunded() => Status = PaymentStatus.Refunded;

    public void MarkAsCancelled() => Status = PaymentStatus.Cancelled;
}
