using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PaymentCartService.Models.Enums;

namespace PaymentCartService.Models;

[Table("carts")]
public class Cart : IHasTimestamps
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("customer_id")]
    [MaxLength(100)]
    public string? CustomerId { get; set; }

    [Column("session_id")]
    [Required]
    [MaxLength(100)]
    public string SessionId { get; set; } = string.Empty;

    [Column("status")]
    [Required]
    [MaxLength(20)]
    public CartStatus Status { get; set; } = CartStatus.Active;

    [Column("expires_at")]
    [Required]
    public DateTime ExpiresAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    public ICollection<CartItem> Items { get; set; } = [];

    public void AddItem(string productId, string productName, string sku, decimal unitPrice, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var existing = Items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is not null)
        {
            existing.UpdateQuantity(existing.Quantity + quantity);
            return;
        }

        Items.Add(new CartItem
        {
            CartId = Id,
            ProductId = productId,
            ProductName = productName,
            Sku = sku,
            UnitPrice = unitPrice,
            Quantity = quantity,
            Subtotal = unitPrice * quantity
        });
    }

    public void UpdateItemQuantity(string itemId, int quantity)
    {
        var item = Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"CartItem {itemId} not found");
        item.UpdateQuantity(quantity);
    }

    public void RemoveItem(string itemId)
    {
        var item = Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"CartItem {itemId} not found");
        Items.Remove(item);
    }

    public void ClearItems() => Items.Clear();

    public decimal CalculateTotal() => Items.Sum(i => i.Subtotal);

    public void MarkAsCheckedOut() => Status = CartStatus.CheckedOut;

    public void MarkAsExpired() => Status = CartStatus.Expired;

    public void MarkAsAbandoned() => Status = CartStatus.Abandoned;
}
