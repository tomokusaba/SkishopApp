namespace InventoryManagementService.Events;

/// <summary>
/// 注文完了イベント。SalesManagementService から Kafka トピック "order.completed" 経由で受信する。
/// 受信時に予約済み在庫を出庫確定（StockOut）し、予約を解放する。
/// </summary>
public record OrderCompletedEvent(
    string OrderId, List<OrderCompletedEvent.OrderCompletedItem> Items,
    DateTimeOffset OccurredAt)
{
    /// <summary>完了した注文明細の商品情報。</summary>
    public record OrderCompletedItem(string ProductId, int Quantity);
}
