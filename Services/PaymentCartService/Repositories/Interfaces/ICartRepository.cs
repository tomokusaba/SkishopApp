using PaymentCartService.Models;

namespace PaymentCartService.Repositories.Interfaces;

public interface ICartRepository
{
    Task<Cart?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<Cart?> FindByIdWithItemsAsync(string id, CancellationToken ct = default);
    Task<Cart?> FindBySessionIdAsync(string sessionId, CancellationToken ct = default);
    Task<Cart?> FindActiveByCustomerIdAsync(string customerId, CancellationToken ct = default);
    Task AddAsync(Cart cart, CancellationToken ct = default);
    Task<int> ExpireCartsAsync(DateTime cutoffDate, int batchSize, CancellationToken ct = default);
    Task<int> CleanupExpiredCartsAsync(DateTime cutoffDate, int batchSize, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
