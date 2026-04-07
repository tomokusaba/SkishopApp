using Frontend.DTOs;

namespace Frontend.Services.Interfaces;

/// <summary>
/// カート API クライアントインターフェース
/// </summary>
public interface ICartApiClient
{
    Task<CartDto?> GetCartAsync(CancellationToken ct = default);
    Task<CartDto?> GetCartByIdAsync(string cartId, CancellationToken ct = default);
    Task AddItemAsync(AddCartItemRequest request, CancellationToken ct = default);
    Task UpdateItemQuantityAsync(string itemId, int quantity, CancellationToken ct = default);
    Task RemoveItemAsync(string itemId, CancellationToken ct = default);
    Task ClearCartAsync(CancellationToken ct = default);
    Task MergeCartAsync(CancellationToken ct = default);
}
