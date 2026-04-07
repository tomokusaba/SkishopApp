namespace Frontend.Store.CartStore;

/// <summary>
/// カート操作のアクション群
/// §6 準拠
/// </summary>
public record AddToCartAction(string ProductId, string ProductName, decimal Price, int Quantity = 1, string? ImageUrl = null, int StockQuantity = 0);
public record RemoveFromCartAction(string ProductId);
public record UpdateCartItemQuantityAction(string ProductId, int Quantity);
public record ClearCartAction;
public record LoadCartAction;
public record LoadCartSuccessAction(List<CartItem> Items);
public record LoadCartFailureAction(string ErrorMessage);
public record MergeCartAction(string UserId);
