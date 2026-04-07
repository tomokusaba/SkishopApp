namespace InventoryManagementService.Events;

/// <summary>
/// 商品更新イベント。Kafka トピック "inventory.product.updated" に発行される。
/// 商品情報（名称・ブランド・カテゴリ・有効状態等）が変更された際に発火する。
/// </summary>
public record ProductUpdatedEvent(
    string ProductId, string Sku, string Name, string? Brand,
    string? CategoryId, bool IsActive, DateTimeOffset UpdatedAt);
