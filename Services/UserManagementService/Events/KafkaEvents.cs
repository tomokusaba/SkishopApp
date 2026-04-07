namespace UserManagementService.Events;

// ── 購読イベント（他マイクロサービスから受信） ──

/// <summary>AuthService からのユーザー登録完了イベント。トピック: <c>user.registered</c></summary>
public record UserRegisteredEvent(
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    DateTimeOffset RegisteredAt);

/// <summary>SalesManagementService からの注文確定イベント。トピック: <c>order.confirmed</c></summary>
public record OrderConfirmedEvent(
    string OrderId,
    string UserId,
    decimal TotalAmount,
    DateTimeOffset ConfirmedAt);

/// <summary>InventoryManagementService からの在庫復活イベント。トピック: <c>inventory.stock-updated</c></summary>
public record InventoryStockUpdatedEvent(
    string ProductId,
    int PreviousQuantity,
    int NewQuantity,
    DateTimeOffset UpdatedAt);

/// <summary>各サービスからのユーザー削除完了通知。トピック: <c>user.deletion.completed</c></summary>
public record UserDeletionCompletedEvent(
    string UserId,
    string ServiceName,
    bool Success,
    string? Error,
    DateTimeOffset CompletedAt);

/// <summary>AuthService からのパスワード変更イベント。トピック: <c>user.password-changed</c></summary>
public record PasswordChangedEvent(
    string UserId,
    DateTimeOffset ChangedAt);

// ── 発行イベント（Outbox ペイロード用） ──

/// <summary>ユーザー削除イベントペイロード。トピック: <c>user.deleted</c></summary>
public record UserDeletedEventPayload(
    string UserId,
    DateTimeOffset DeletedAt);

/// <summary>プロフィル更新イベントペイロード。トピック: <c>user.profile-updated</c></summary>
public record UserProfileUpdatedEventPayload(
    string UserId,
    List<string> UpdatedFields,
    DateTimeOffset UpdatedAt);

/// <summary>同意撤回イベントペイロード。トピック: <c>consent.revoked</c></summary>
public record ConsentRevokedEventPayload(
    string UserId,
    string ConsentType,
    DateTimeOffset RevokedAt);

/// <summary>会員ランク更新イベントペイロード。トピック: <c>member-rank.updated</c></summary>
public record MemberRankUpdatedEventPayload(
    string UserId,
    string PreviousRank,
    string NewRank,
    decimal PointRate,
    DateTimeOffset UpdatedAt);

/// <summary>削除完了通知イベントペイロード。トピック: <c>user.deletion.notification</c></summary>
public record UserDeletionNotificationEventPayload(
    string UserId,
    DateTimeOffset CompletedAt);
