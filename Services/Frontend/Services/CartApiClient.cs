using Frontend.DTOs;
using Frontend.Services.Interfaces;

namespace Frontend.Services;

public class CartApiClient(IApiGatewayClient apiClient, ILogger<CartApiClient> logger) : ICartApiClient
{
    public async Task<CartDto?> GetCartAsync(CancellationToken ct = default)
    {
        try
        {
            return await apiClient.GetAsync<CartDto>("/api/v1/cart", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "カート取得失敗");
            return null;
        }
    }

    public async Task<CartDto?> GetCartByIdAsync(string cartId, CancellationToken ct = default)
    {
        return await apiClient.GetAsync<CartDto>($"/api/v1/cart/{cartId}", ct);
    }

    public async Task AddItemAsync(AddCartItemRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("カートアイテム追加: ProductId={ProductId}", request.ProductId);
        await apiClient.PostAsync<AddCartItemRequest, object>("/api/v1/cart/items", request, ct);
    }

    public async Task UpdateItemQuantityAsync(string itemId, int quantity, CancellationToken ct = default)
    {
        logger.LogInformation("カートアイテム数量変更: ItemId={ItemId}, Quantity={Quantity}", itemId, quantity);
        await apiClient.PutAsync<UpdateCartItemRequest, object>(
            $"/api/v1/cart/items/{itemId}", new UpdateCartItemRequest(quantity), ct);
    }

    public async Task RemoveItemAsync(string itemId, CancellationToken ct = default)
    {
        logger.LogInformation("カートアイテム削除: ItemId={ItemId}", itemId);
        await apiClient.DeleteAsync($"/api/v1/cart/items/{itemId}", ct);
    }

    public async Task ClearCartAsync(CancellationToken ct = default)
    {
        logger.LogInformation("カート全クリア");
        await apiClient.DeleteAsync("/api/v1/cart", ct);
    }

    public async Task MergeCartAsync(CancellationToken ct = default)
    {
        logger.LogInformation("カートマージ実行");
        await apiClient.PostAsync<object, object>("/api/v1/cart/merge", new { }, ct);
    }
}

