namespace PaymentCartService.DTOs.Responses;

/// <summary>
/// カート内の商品アイテム情報
/// </summary>
/// <param name="Id">カートアイテム ID</param>
/// <param name="ProductId">商品 ID</param>
/// <param name="ProductName">商品名</param>
/// <param name="Sku">SKU コード</param>
/// <param name="UnitPrice">単価</param>
/// <param name="Quantity">数量</param>
/// <param name="Subtotal">小計</param>
/// <param name="CreatedAt">追加日時</param>
public record CartItemResponse(
    string Id,
    string ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal,
    DateTime CreatedAt);
