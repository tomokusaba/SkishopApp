using PaymentCartService.Models;

namespace PaymentCartService.Services.Interfaces;

public interface ICartCacheService
{
    Task<Cart?> GetCartAsync(string cartId, CancellationToken ct = default);
    Task SetCartAsync(Cart cart, CancellationToken ct = default);
    Task RemoveCartAsync(string cartId, CancellationToken ct = default);
    Task<string?> GetCartIdBySessionAsync(string sessionId, CancellationToken ct = default);
    Task SetCartIdBySessionAsync(string sessionId, string cartId, CancellationToken ct = default);
    Task<string?> GetCartIdByUserAsync(string customerId, CancellationToken ct = default);
    Task SetCartIdByUserAsync(string customerId, string cartId, CancellationToken ct = default);
}
