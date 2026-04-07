namespace InventoryManagementService.Events;

/// <summary>
/// 注文作成イベント。SalesManagementService から Kafka トピック "order.created" 経由で受信する。
/// 受信時に注文内の各商品について在庫予約（Reserve）を実行する。
/// </summary>
public record OrderCreatedEvent(
    string OrderId, string UserId, List<OrderCreatedEvent.OrderItem> Items,
    DateTimeOffset OccurredAt)
{
    /// <summary>注文明細の商品情報。</summary>
    public record OrderItem(string ProductId, int Quantity, decimal UnitPrice);
}
