using Microsoft.EntityFrameworkCore;
using PaymentCartService.Infrastructure.Persistence;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;

namespace PaymentCartService.Repositories;

public class CartRepository(AppDbContext context) : ICartRepository
{
    public async Task<Cart?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Carts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Cart?> FindByIdWithItemsAsync(string id, CancellationToken ct = default)
        => await context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Cart?> FindBySessionIdAsync(string sessionId, CancellationToken ct = default)
        => await context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.SessionId == sessionId
                && c.Status == CartStatus.Active, ct);

    public async Task<Cart?> FindActiveByCustomerIdAsync(string customerId, CancellationToken ct = default)
        => await context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId
                && c.Status == CartStatus.Active, ct);

    public async Task AddAsync(Cart cart, CancellationToken ct = default)
        => await context.Carts.AddAsync(cart, ct);

    public async Task<int> ExpireCartsAsync(DateTime cutoffDate, int batchSize, CancellationToken ct = default)
        => await context.Carts
            .Where(c => c.Status == CartStatus.Active && c.ExpiresAt < cutoffDate)
            .OrderBy(c => c.Id)
            .Take(batchSize)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, CartStatus.Expired), ct);

    public async Task<int> CleanupExpiredCartsAsync(DateTime cutoffDate, int batchSize, CancellationToken ct = default)
        => await context.Carts
            .Where(c => (c.Status == CartStatus.Expired || c.Status == CartStatus.Abandoned)
                && c.UpdatedAt < cutoffDate)
            .OrderBy(c => c.Id)
            .Take(batchSize)
            .ExecuteDeleteAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
