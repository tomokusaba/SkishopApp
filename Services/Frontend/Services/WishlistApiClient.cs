using Frontend.DTOs;
using Frontend.Services.Interfaces;

namespace Frontend.Services;

public class WishlistApiClient(IApiGatewayClient apiClient, ILogger<WishlistApiClient> logger) : IWishlistApiClient
{
    private const string UserId = "me";

    public async Task<List<WishlistDto>> GetWishlistsAsync(CancellationToken ct = default)
    {
        logger.LogInformation("ウィッシュリスト一覧取得");
        var result = await apiClient.GetAsync<List<WishlistDto>>(
            $"/api/v1/users/{UserId}/wishlists", ct);
        return result ?? [];
    }

    public async Task<WishlistDto?> CreateWishlistAsync(CreateWishlistRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("ウィッシュリスト作成: Name={Name}", request.Name);
        return await apiClient.PostAsync<CreateWishlistRequest, WishlistDto>(
            $"/api/v1/users/{UserId}/wishlists", request, ct);
    }

    public async Task<WishlistDto?> UpdateWishlistAsync(string id, UpdateWishlistRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("ウィッシュリスト更新: Id={WishlistId}", id);
        return await apiClient.PutAsync<UpdateWishlistRequest, WishlistDto>(
            $"/api/v1/users/{UserId}/wishlists/{id}", request, ct);
    }

    public async Task<WishlistDto?> AddItemToWishlistAsync(string wishlistId, AddWishlistItemRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("ウィッシュリストアイテム追加: WishlistId={WishlistId}, ProductId={ProductId}",
            wishlistId, request.ProductId);
        return await apiClient.PostAsync<AddWishlistItemRequest, WishlistDto>(
            $"/api/v1/users/{UserId}/wishlists/{wishlistId}/items", request, ct);
    }

    public async Task RemoveItemFromWishlistAsync(string wishlistId, string itemId, CancellationToken ct = default)
    {
        logger.LogInformation("ウィッシュリストアイテム削除: WishlistId={WishlistId}, ItemId={ItemId}",
            wishlistId, itemId);
        await apiClient.DeleteAsync(
            $"/api/v1/users/{UserId}/wishlists/{wishlistId}/items/{itemId}", ct);
    }

    public async Task DeleteWishlistAsync(string id, CancellationToken ct = default)
    {
        logger.LogInformation("ウィッシュリスト削除: Id={WishlistId}", id);
        await apiClient.DeleteAsync($"/api/v1/users/{UserId}/wishlists/{id}", ct);
    }
}

