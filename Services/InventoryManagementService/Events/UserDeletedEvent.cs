namespace InventoryManagementService.Events;

/// <summary>
/// ユーザー削除イベント。UserManagementService から Kafka トピック "user.deleted" 経由で受信する。
/// 受信時に該当ユーザーのレビューを匿名化する。
/// </summary>
public record UserDeletedEvent(string UserId, DateTimeOffset DeletedAt);
