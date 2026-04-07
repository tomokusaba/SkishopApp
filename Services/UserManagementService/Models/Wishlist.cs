using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UserManagementService.Exceptions;

namespace UserManagementService.Models;

/// <summary>
/// ウィッシュリスト Aggregate Root。1 リストあたり最大 100 アイテム、ユーザーあたり最大 10 リストまで作成可能。
/// アイテムの追加/削除は Wishlist 経由でのみ行う（Aggregate 境界保護）。
/// </summary>
[Table("wishlists")]
public class Wishlist
{
    private const int MaxItems = 100;

    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    [Column("name")]
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("is_default")]
    public bool IsDefault { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<WishlistItem> Items { get; set; } = [];

    public void Rename(string name) => Name = name;

    public void MarkAsDefault()
        => IsDefault = true;

    /// <summary>
    /// ウィッシュリストに商品を追加する。重複・上限チェックを行う。
    /// </summary>
    /// <exception cref="BusinessException">アイテム上限超過または重複登録時。</exception>
    public WishlistItem AddItem(string productId, bool notifyOnRestock)
    {
        if (Items.Count >= MaxItems)
            throw new BusinessException($"ウィッシュリストのアイテムは最大 {MaxItems} 件までです");

        if (Items.Any(item => item.ProductId == productId))
            throw new BusinessException("同じ商品は既にウィッシュリストに登録されています");

        var item = new WishlistItem
        {
            WishlistId = Id,
            ProductId = productId,
            ShouldNotifyOnRestock = notifyOnRestock
        };
        Items.Add(item);
        return item;
    }

    /// <summary>
    /// ウィッシュリストからアイテムを削除する。
    /// </summary>
    /// <exception cref="NotFoundException">指定 ID のアイテムが存在しない場合。</exception>
    public WishlistItem RemoveItem(string itemId)
    {
        var item = Items.FirstOrDefault(current => current.Id == itemId)
            ?? throw new NotFoundException($"アイテムが見つかりません (ID: {itemId})");
        Items.Remove(item);
        return item;
    }
}
