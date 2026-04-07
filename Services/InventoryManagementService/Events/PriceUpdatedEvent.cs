namespace InventoryManagementService.Events;

/// <summary>
/// 価格変更イベント。Kafka トピック "inventory.price.updated" に発行される。
/// 通常価格またはセール価格が変更された際に発火する。
/// </summary>
public record PriceUpdatedEvent(
    string ProductId, decimal OldRegularPrice, decimal NewRegularPrice,
    decimal? OldSalePrice, decimal? NewSalePrice,
    string CurrencyCode, DateTimeOffset UpdatedAt);
