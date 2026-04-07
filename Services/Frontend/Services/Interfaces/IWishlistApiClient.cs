using Frontend.DTOs;

namespace Frontend.Services.Interfaces;

/// <summary>
/// ウィッシュリスト API クライアントインターフェース
/// </summary>
public interface IWishlistApiClient
{
    Task<List<WishlistDto>> GetWishlistsAsync(CancellationToken ct = default);
    Task<WishlistDto?> CreateWishlistAsync(CreateWishlistRequest request, CancellationToken ct = default);
    Task<WishlistDto?> UpdateWishlistAsync(string id, UpdateWishlistRequest request, CancellationToken ct = default);
    Task<WishlistDto?> AddItemToWishlistAsync(string wishlistId, AddWishlistItemRequest request, CancellationToken ct = default);
    Task RemoveItemFromWishlistAsync(string wishlistId, string itemId, CancellationToken ct = default);
    Task DeleteWishlistAsync(string id, CancellationToken ct = default);
}
