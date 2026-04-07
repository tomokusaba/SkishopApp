using UserManagementService.Models;

namespace UserManagementService.Repositories.Interfaces;

/// <summary>
/// ウィッシュリスト Repository。アイテムを含む Eager Loading クエリを提供する。
/// </summary>
public interface IWishlistRepository
{
    Task<Wishlist?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<Wishlist?> FindByIdWithItemsAsync(string id, CancellationToken ct = default);
    Task<List<Wishlist>> FindByUserIdAsync(string userId, CancellationToken ct = default);
    Task<int> CountByUserIdAsync(string userId, CancellationToken ct = default);
    Task AddAsync(Wishlist wishlist, CancellationToken ct = default);
    void Remove(Wishlist wishlist);
    Task<List<Wishlist>> FindByProductIdWithRestockNotifyAsync(string productId, int batchSize, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
