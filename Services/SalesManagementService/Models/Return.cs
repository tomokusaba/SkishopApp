using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SalesManagementService.Models;

[Table("returns")]
public class Return
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("return_number")]
    [Required]
    [MaxLength(50)]
    public string ReturnNumber { get; set; } = string.Empty;

    [Column("order_id")]
    [Required]
    [MaxLength(36)]
    public string OrderId { get; set; } = string.Empty;

    [Column("order_item_id")]
    [Required]
    [MaxLength(36)]
    public string OrderItemId { get; set; } = string.Empty;

    [Column("customer_id")]
    [Required]
    [MaxLength(100)]
    public string CustomerId { get; set; } = string.Empty;

    [Column("reason")]
    [Required]
    [MaxLength(30)]
    public string Reason { get; set; } = string.Empty;

    [Column("reason_detail")]
    public string? ReasonDetail { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("refund_amount")]
    [Precision(12, 2)]
    public decimal RefundAmount { get; set; }

    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "REQUESTED";

    [Column("requested_at")]
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("approved_at")]
    public DateTimeOffset? ApprovedAt { get; set; }

    [Column("received_at")]
    public DateTimeOffset? ReceivedAt { get; set; }

    [Column("refunded_at")]
    public DateTimeOffset? RefundedAt { get; set; }

    [Column("admin_notes")]
    public string? AdminNotes { get; set; }

    [Column("created_by")]
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [Column("updated_by")]
    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    // ── ナビゲーションプロパティ ──
    public Order Order { get; set; } = null!;
    public OrderItem OrderItem { get; set; } = null!;
}
