namespace InventoryManagementService.Events;

/// <summary>
/// 在庫数量変更イベント。Kafka トピック "inventory.updated" に発行される。
/// 入庫・出庫・在庫調整により物理在庫数が変更された際に発火する。
/// </summary>
public record InventoryUpdatedEvent(
    string ProductId, string Sku, int PreviousQuantity, int NewQuantity,
    string Reason, string LocationCode, DateTimeOffset UpdatedAt,
    string? ReferenceId = null);
