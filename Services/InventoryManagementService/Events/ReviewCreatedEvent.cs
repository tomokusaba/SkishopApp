namespace InventoryManagementService.Events;

/// <summary>
/// レビュー作成イベント。Kafka トピック "inventory.review.created" に発行される。
/// 新しいカスタマーレビューが投稿された際に発火する。
/// </summary>
public record ReviewCreatedEvent(
    string ReviewId, string ProductId, string UserId,
    int Rating, DateTimeOffset CreatedAt);
