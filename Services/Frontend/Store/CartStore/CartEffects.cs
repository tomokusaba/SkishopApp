using Fluxor;
using Frontend.DTOs;
using Frontend.Services;
using Frontend.Services.Interfaces;

namespace Frontend.Store.CartStore;

/// <summary>
/// カートの副作用処理（API 通信）
/// §6 準拠 — CartApiClient 経由でカート操作
/// H-12: IApiGatewayClient 直接使用を CartApiClient に変更
/// </summary>
public class CartEffects(
    ICartApiClient cartApiClient,
    ILogger<CartEffects> logger)
{
    [EffectMethod]
    public async Task HandleLoadCart(LoadCartAction _, IDispatcher dispatcher)
    {
        try
        {
            var cart = await cartApiClient.GetCartAsync();
            var items = cart?.Items.Select(i => new CartItem(
                i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.ImageUrl)).ToList() ?? [];
            dispatcher.Dispatch(new LoadCartSuccessAction(items));
        }
        catch (UnauthorizedAccessException)
        {
            dispatcher.Dispatch(new LoadCartSuccessAction([]));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "カート読み込みエラー");
            dispatcher.Dispatch(new LoadCartFailureAction("カートの読み込みに失敗しました"));
        }
    }

    [EffectMethod]
    public async Task HandleAddToCart(AddToCartAction action, IDispatcher dispatcher)
    {
        try
        {
            await cartApiClient.AddItemAsync(new AddCartItemRequest(action.ProductId, action.Quantity));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "カート追加エラー: ProductId={ProductId}", action.ProductId);
        }
    }

    [EffectMethod]
    public async Task HandleRemoveFromCart(RemoveFromCartAction action, IDispatcher dispatcher)
    {
        try
        {
            await cartApiClient.RemoveItemAsync(action.ProductId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "カート削除エラー: ProductId={ProductId}", action.ProductId);
        }
    }
}
