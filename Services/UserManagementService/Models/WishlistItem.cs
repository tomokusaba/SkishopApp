using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserManagementService.Models;

/// <summary>
/// ウィッシュリスト内の個別アイテム。Wishlist Aggregate の子エンティティ。
/// 在庫復活通知（<see cref="ShouldNotifyOnRestock"/>）をサポートする。
/// </summary>
[Table("wishlist_items")]
public class WishlistItem
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("wishlist_id")]
    [Required]
    [MaxLength(36)]
    public string WishlistId { get; set; } = string.Empty;

    [Column("product_id")]
    [Required]
    [MaxLength(36)]
    public string ProductId { get; set; } = string.Empty;

    [Column("added_at")]
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("notify_on_restock")]
    public bool ShouldNotifyOnRestock { get; set; }

    [Column("notified_at")]
    public DateTimeOffset? NotifiedAt { get; set; }

    public Wishlist Wishlist { get; set; } = null!;
}
