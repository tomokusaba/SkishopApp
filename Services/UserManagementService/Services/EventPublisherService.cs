using System.Text.Json;
using UserManagementService.Events;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Services;

/// <summary>
/// Outbox パターンによるイベント発行サービス。各イベントを OutboxEvent テーブルに書き込み、
/// <see cref="BackgroundServices.OutboxPublisher"/> が非同期で Kafka に発行する。
/// </summary>
public class EventPublisherService(
    IOutboxEventRepository outboxRepository,
    TimeProvider timeProvider,
    ILogger<EventPublisherService> logger) : IEventPublisherService
{
    public async Task PublishProfileUpdatedAsync(string userId, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var payload = new UserProfileUpdatedEventPayload(
            userId, ["firstName", "lastName", "phoneNumber", "birthDate"],
            now);

        var outboxEvent = new OutboxEvent
        {
            EventType = "user.profile-updated",
            AggregateId = userId,
            Payload = JsonSerializer.Serialize(payload)
        };
        await outboxRepository.AddAsync(outboxEvent, ct);
        logger.LogInformation("Outbox イベント追加: {EventType}, {UserId}",
            outboxEvent.EventType, userId);
    }

    public async Task PublishUserDeletedAsync(string userId, CancellationToken ct = default)
    {
        var payload = new UserDeletedEventPayload(userId, timeProvider.GetUtcNow());
        var outboxEvent = new OutboxEvent
        {
            EventType = "user.deleted",
            AggregateId = userId,
            Payload = JsonSerializer.Serialize(payload)
        };
        await outboxRepository.AddAsync(outboxEvent, ct);
        logger.LogInformation("Outbox イベント追加: {EventType}, {UserId}",
            outboxEvent.EventType, userId);
    }

    public async Task PublishConsentRevokedAsync(
        string userId, string consentType, CancellationToken ct = default)
    {
        var payload = new ConsentRevokedEventPayload(userId, consentType, timeProvider.GetUtcNow());
        var outboxEvent = new OutboxEvent
        {
            EventType = "consent.revoked",
            AggregateId = userId,
            Payload = JsonSerializer.Serialize(payload)
        };
        await outboxRepository.AddAsync(outboxEvent, ct);
        logger.LogInformation("Outbox イベント追加: {EventType}, {UserId}",
            outboxEvent.EventType, userId);
    }

    public async Task PublishMemberRankUpdatedAsync(
        string userId, string previousRank, string newRank, decimal pointRate,
        CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var payload = new MemberRankUpdatedEventPayload(
            userId, previousRank, newRank, pointRate, now);
        var outboxEvent = new OutboxEvent
        {
            EventType = "member-rank.updated",
            AggregateId = userId,
            Payload = JsonSerializer.Serialize(payload)
        };
        await outboxRepository.AddAsync(outboxEvent, ct);
        logger.LogInformation("Outbox イベント追加: {EventType}, {UserId}, {PreviousRank} → {NewRank}",
            outboxEvent.EventType, userId, previousRank, newRank);
    }

    public async Task PublishDeletionNotificationAsync(
        string userId, CancellationToken ct = default)
    {
        var payload = new UserDeletionNotificationEventPayload(
            userId, timeProvider.GetUtcNow());
        var outboxEvent = new OutboxEvent
        {
            EventType = "user.deletion.notification",
            AggregateId = userId,
            Payload = JsonSerializer.Serialize(payload)
        };
        await outboxRepository.AddAsync(outboxEvent, ct);
        logger.LogInformation("Outbox イベント追加: {EventType}, {UserId}",
            outboxEvent.EventType, userId);
    }
}
