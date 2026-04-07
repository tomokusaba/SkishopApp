using PaymentCartService.DTOs.Requests;
using PaymentCartService.DTOs.Responses;

namespace PaymentCartService.Services.Interfaces;

public interface ICartService
{
    Task<CartResponse> GetCartAsync(string cartId, CancellationToken ct = default);
    Task<CartResponse> GetOrCreateCartAsync(string? cartId, string sessionId, CancellationToken ct = default);
    Task<CartResponse> AddItemAsync(string cartId, AddCartItemRequest request, CancellationToken ct = default);
    Task<CartResponse> UpdateItemQuantityAsync(
        string cartId, string itemId, UpdateCartItemRequest request, CancellationToken ct = default);
    Task<CartResponse> RemoveItemAsync(string cartId, string itemId, CancellationToken ct = default);
    Task ClearCartAsync(string cartId, CancellationToken ct = default);
    Task<CartResponse> MergeCartAsync(string guestCartId, string userId, CancellationToken ct = default);
}
