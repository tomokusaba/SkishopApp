namespace InventoryManagementService.Events;

/// <summary>
/// 商品新規作成イベント。Kafka トピック "inventory.product.created" に発行される。
/// 商品カタログに新しい商品が登録された際に発火する。
/// </summary>
public record ProductCreatedEvent(
    string ProductId, string Sku, string Name, string? Brand,
    string? CategoryId, DateTimeOffset CreatedAt);
