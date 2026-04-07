namespace InventoryManagementService.Events;

/// <summary>
/// 注文キャンセルイベント。SalesManagementService から Kafka トピック "order.cancelled" 経由で受信する。
/// 受信時に予約済み在庫を解放（Release）する。
/// </summary>
public record OrderCancelledEvent(
    string OrderId, string ReservationId, string Reason,
    List<OrderCancelledEvent.CancelledItem> Items, DateTimeOffset CancelledAt)
{
    /// <summary>キャンセルされた注文明細の商品情報。</summary>
    public record CancelledItem(string ProductId, int Quantity);
}
