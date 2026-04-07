namespace InventoryManagementService.Events;

/// <summary>
/// レビュー承認イベント。Kafka トピック "inventory.review.approved" に発行される。
/// レビューが管理者によって承認された際に発火する。
/// </summary>
public record ReviewApprovedEvent(
    string ReviewId, string ProductId, int Rating,
    DateTimeOffset ApprovedAt);
