namespace InventoryManagementService.Events;

/// <summary>
/// 商品削除イベント。Kafka トピック "inventory.product.deleted" に発行される。
/// 商品がカタログから削除（論理削除）された際に発火する。
/// </summary>
public record ProductDeletedEvent(
    string ProductId, string Sku, DateTimeOffset DeletedAt);
