namespace InventoryManagementService.Events;

/// <summary>
/// 在庫予約解放イベント。Kafka トピック "inventory.released" に発行される。
/// 注文キャンセルや Saga の補償トランザクションで、予約済み在庫が解放された際に発火する。
/// </summary>
public record InventoryReleasedEvent(
    string OrderId, string ProductId, int Quantity,
    string ReservationId, string Reason, DateTimeOffset ReleasedAt);
