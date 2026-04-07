using Microsoft.EntityFrameworkCore;
using UserManagementService.Infrastructure.Persistence;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;

namespace UserManagementService.Repositories;

/// <summary>
/// ウィッシュリストの EF Core Repository 実装。Items の Eager Loading と在庫通知対象検索を提供する。
/// </summary>
public class WishlistRepository(AppDbContext context) : IWishlistRepository
{
    public async Task<Wishlist?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Wishlists.FirstOrDefaultAsync(w => w.Id == id, ct);

    public async Task<Wishlist?> FindByIdWithItemsAsync(string id, CancellationToken ct = default)
        => await context.Wishlists
            .Include(w => w.Items)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

    public async Task<List<Wishlist>> FindByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.Wishlists
            .AsNoTracking()
            .Include(w => w.Items)
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.IsDefault)
            .ThenByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

    public async Task<int> CountByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.Wishlists.CountAsync(w => w.UserId == userId, ct);

    public async Task AddAsync(Wishlist wishlist, CancellationToken ct = default)
        => await context.Wishlists.AddAsync(wishlist, ct);

    public void Remove(Wishlist wishlist)
        => context.Wishlists.Remove(wishlist);

    public async Task<List<Wishlist>> FindByProductIdWithRestockNotifyAsync(
        string productId, int batchSize, CancellationToken ct = default)
        => await context.Wishlists
            .AsNoTracking()
            .Include(w => w.Items.Where(i => i.ProductId == productId && i.ShouldNotifyOnRestock && i.NotifiedAt == null))
            .Where(w => w.Items.Any(i => i.ProductId == productId && i.ShouldNotifyOnRestock && i.NotifiedAt == null))
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
