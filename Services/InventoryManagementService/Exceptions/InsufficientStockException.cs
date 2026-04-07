namespace InventoryManagementService.Exceptions;

/// <summary>
/// 在庫不足の場合にスローされる例外。HTTP 422 Unprocessable Entity にマッピングされる。
/// 要求数量と利用可能数量を Details に含め、クライアントに不足情報を提供する。
/// </summary>
/// <param name="productId">在庫不足が発生した商品の ID。</param>
/// <param name="requested">要求された数量。</param>
/// <param name="available">利用可能な数量（物理在庫 − 予約済み数量）。</param>
public class InsufficientStockException(string productId, int requested, int available)
    : InventoryException("INV_001", "在庫不足",
        new Dictionary<string, object>
        {
            ["productId"] = productId,
            ["requestedQuantity"] = requested,
            ["availableQuantity"] = available
        });
