using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SalesManagementService.Models;

[Table("orders")]
public class Order
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("order_number")]
    [Required]
    [MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    [Column("customer_id")]
    [Required]
    [MaxLength(100)]
    public string CustomerId { get; set; } = string.Empty;

    [Column("is_guest")]
    public bool IsGuest { get; set; }

    [Column("guest_email")]
    [MaxLength(255)]
    public string? GuestEmail { get; set; }

    [Column("order_date")]
    public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.UtcNow;

    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "PENDING";

    [Column("payment_status")]
    [Required]
    [MaxLength(20)]
    public string PaymentStatus { get; set; } = "PENDING";

    [Column("payment_method")]
    [Required]
    [MaxLength(50)]
    public string PaymentMethod { get; set; } = string.Empty;

    [Column("subtotal_amount")]
    [Precision(12, 2)]
    public decimal SubtotalAmount { get; set; }

    [Column("tax_amount")]
    [Precision(12, 2)]
    public decimal TaxAmount { get; set; }

    [Column("shipping_fee")]
    [Precision(12, 2)]
    public decimal ShippingFee { get; set; }

    [Column("discount_amount")]
    [Precision(12, 2)]
    public decimal DiscountAmount { get; set; }

    [Column("total_amount")]
    [Precision(12, 2)]
    public decimal TotalAmount { get; set; }

    [Column("coupon_code")]
    [MaxLength(50)]
    public string? CouponCode { get; set; }

    [Column("used_points")]
    public int UsedPoints { get; set; }

    [Column("point_discount_amount")]
    [Precision(12, 2)]
    public decimal PointDiscountAmount { get; set; }

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

    [Column("currency_code")]
    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "JPY";

    [Column("notes")]
    public string? Notes { get; set; }

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
    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<Shipment> Shipments { get; set; } = [];
    public ICollection<Return> Returns { get; set; } = [];
    public Invoice? Invoice { get; set; }
    public SagaLog? SagaLog { get; set; }

    // ── ドメインメソッド（Aggregate Root） ──
    public void AddItem(string productId, string productName, string sku,
        decimal unitPrice, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        Items.Add(new OrderItem
        {
            OrderId = Id,
            ProductId = productId,
            ProductName = productName,
            Sku = sku,
            UnitPrice = unitPrice,
            Quantity = quantity,
            Subtotal = unitPrice * quantity
        });
    }
}
