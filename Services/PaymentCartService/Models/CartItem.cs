using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PaymentCartService.Models;

[Table("cart_items")]
public class CartItem : IHasTimestamps
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("cart_id")]
    [Required]
    [MaxLength(36)]
    public string CartId { get; set; } = string.Empty;

    [Column("product_id")]
    [Required]
    [MaxLength(100)]
    public string ProductId { get; set; } = string.Empty;

    [Column("product_name")]
    [Required]
    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [Column("sku")]
    [Required]
    [MaxLength(100)]
    public string Sku { get; set; } = string.Empty;

    [Column("unit_price", TypeName = "decimal(12,2)")]
    [Required]
    public decimal UnitPrice { get; set; }

    [Column("quantity")]
    [Required]
    public int Quantity { get; set; }

    [Column("subtotal", TypeName = "decimal(12,2)")]
    [Required]
    public decimal Subtotal { get; set; }

    // AddedAt から CreatedAt にリネーム（IHasTimestamps 準拠）
    // DB マイグレーションで added_at → created_at のリネームが必要
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(CartId))]
    public Cart Cart { get; set; } = null!;

    public void UpdateQuantity(int newQuantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newQuantity);
        Quantity = newQuantity;
        Subtotal = UnitPrice * newQuantity;
    }
}
