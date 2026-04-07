namespace InventoryManagementService.Events;

/// <summary>
/// 低在庫アラートイベント。Kafka トピック "inventory.low-stock-alert" に発行される。
/// 在庫数量が発注点（ReorderPoint）以下になった際に発火する。
/// </summary>
public record LowStockAlertEvent(
    string ProductId, string Sku, int CurrentQuantity, int ReorderPoint,
    string LocationCode, DateTimeOffset AlertedAt);
