using Fluxor;

namespace Frontend.Store.CartStore;

/// <summary>
/// カート状態のリデューサー群
/// §6 準拠 — 純粋関数で状態を更新
/// </summary>
public static class CartReducers
{
    [ReducerMethod]
    public static CartState OnAddToCart(CartState state, AddToCartAction action)
    {
        var items = state.Items.ToList();
        var existing = items.Find(i => i.ProductId == action.ProductId);
        if (existing is not null)
        {
            items.Remove(existing);
            items.Add(existing with { Quantity = existing.Quantity + action.Quantity });
        }
        else
        {
            items.Add(new CartItem(action.ProductId, action.ProductName, action.Price, action.Quantity, action.ImageUrl, action.StockQuantity));
        }
        return state with { Items = items, ErrorMessage = null };
    }

    [ReducerMethod]
    public static CartState OnRemoveFromCart(CartState state, RemoveFromCartAction action)
    {
        var items = state.Items.Where(i => i.ProductId != action.ProductId).ToList();
        return state with { Items = items };
    }

    [ReducerMethod]
    public static CartState OnUpdateQuantity(CartState state, UpdateCartItemQuantityAction action)
    {
        var items = state.Items.ToList();
        var existing = items.Find(i => i.ProductId == action.ProductId);
        if (existing is not null)
        {
            items.Remove(existing);
            if (action.Quantity > 0)
            {
                items.Add(existing with { Quantity = action.Quantity });
            }
        }
        return state with { Items = items };
    }

    [ReducerMethod(typeof(ClearCartAction))]
    public static CartState OnClearCart(CartState state)
        => state with { Items = [] };

    [ReducerMethod(typeof(LoadCartAction))]
    public static CartState OnLoadCart(CartState state)
        => state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static CartState OnLoadCartSuccess(CartState state, LoadCartSuccessAction action)
        => state with { Items = action.Items, IsLoading = false };

    [ReducerMethod]
    public static CartState OnLoadCartFailure(CartState state, LoadCartFailureAction action)
        => state with { IsLoading = false, ErrorMessage = action.ErrorMessage };
}
