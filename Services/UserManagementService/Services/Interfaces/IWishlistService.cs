using UserManagementService.DTOs.Requests;
using UserManagementService.DTOs.Responses;

namespace UserManagementService.Services.Interfaces;

/// <summary>
/// ウィッシュリスト管理サービス。リスト CRUD・アイテム追加削除・カート移動・在庫復活通知を提供する。
/// </summary>
public interface IWishlistService
{
    Task<List<WishlistDto>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<WishlistDto> CreateAsync(string userId, CreateWishlistRequest request, CancellationToken ct = default);
    Task<WishlistDto> UpdateAsync(string userId, string wishlistId, UpdateWishlistRequest request, CancellationToken ct = default);
    Task DeleteAsync(string userId, string wishlistId, CancellationToken ct = default);
    Task<WishlistItemDto> AddItemAsync(string userId, string wishlistId, AddWishlistItemRequest request, CancellationToken ct = default);
    Task RemoveItemAsync(string userId, string wishlistId, string itemId, CancellationToken ct = default);
    Task MoveItemToCartAsync(string userId, string wishlistId, string itemId, CancellationToken ct = default);
    Task ProcessRestockNotificationAsync(string productId, CancellationToken ct = default);
}
