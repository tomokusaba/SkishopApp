namespace InventoryManagementService.Events;

/// <summary>
/// 在庫枯渇イベント。Kafka トピック "inventory.stock-depleted" に発行される。
/// 在庫数量がゼロになった際に発火し、緊急の補充アクションを促す。
/// </summary>
public record StockDepletedEvent(
    string ProductId, string Sku, string LocationCode, DateTimeOffset DepletedAt);
