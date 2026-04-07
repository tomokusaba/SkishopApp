using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesManagementService.Models;

[Table("shipments")]
public class Shipment
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("order_id")]
    [Required]
    [MaxLength(36)]
    public string OrderId { get; set; } = string.Empty;

    [Column("carrier")]
    [Required]
    [MaxLength(100)]
    public string Carrier { get; set; } = string.Empty;

    [Column("tracking_number")]
    [MaxLength(100)]
    public string? TrackingNumber { get; set; }

    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "PREPARING";

    [Column("shipping_postal_code")]
    [MaxLength(10)]
    public string? ShippingPostalCode { get; set; }

    [Column("shipping_prefecture")]
    [MaxLength(50)]
    public string? ShippingPrefecture { get; set; }

    [Column("shipping_city")]
    [MaxLength(100)]
    public string? ShippingCity { get; set; }

    [Column("shipping_address_line1")]
    [MaxLength(200)]
    public string? ShippingAddressLine1 { get; set; }

    [Column("shipping_address_line2")]
    [MaxLength(200)]
    public string? ShippingAddressLine2 { get; set; }

    [Column("shipping_recipient_name")]
    [MaxLength(100)]
    public string? ShippingRecipientName { get; set; }

    [Column("shipping_phone_number")]
    [MaxLength(20)]
    public string? ShippingPhoneNumber { get; set; }

    [Column("shipped_at")]
    public DateTimeOffset? ShippedAt { get; set; }

    [Column("estimated_delivery_at")]
    public DateTimeOffset? EstimatedDeliveryAt { get; set; }

    [Column("delivered_at")]
    public DateTimeOffset? DeliveredAt { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    // ── ナビゲーションプロパティ ──
    public Order Order { get; set; } = null!;
}
