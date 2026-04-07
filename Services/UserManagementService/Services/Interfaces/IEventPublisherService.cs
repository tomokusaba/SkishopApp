namespace UserManagementService.Services.Interfaces;

/// <summary>
/// Outbox パターンによるイベント発行サービス。
/// DB トランザクション内で <c>outbox_events</c> テーブルにイベントを書き込み、
/// <see cref="BackgroundServices.OutboxPublisher"/> が非同期で Kafka へ発行する。
/// </summary>
public interface IEventPublisherService
{
    Task PublishProfileUpdatedAsync(string userId, CancellationToken ct = default);
    Task PublishUserDeletedAsync(string userId, CancellationToken ct = default);
    Task PublishConsentRevokedAsync(string userId, string consentType, CancellationToken ct = default);
    Task PublishMemberRankUpdatedAsync(string userId, string previousRank, string newRank, decimal pointRate, CancellationToken ct = default);
    Task PublishDeletionNotificationAsync(string userId, CancellationToken ct = default);
}
