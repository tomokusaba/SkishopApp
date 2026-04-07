namespace InventoryManagementService.Events;

/// <summary>
/// 在庫予約完了イベント。Kafka トピック "inventory.reserved" に発行される。
/// 注文確定フロー（Saga）の在庫引当ステップで、在庫の予約が成功した際に発火する。
/// </summary>
public record InventoryReservedEvent(
    string OrderId, string ProductId, int Quantity,
    string ReservationId, DateTimeOffset ReservedAt);
