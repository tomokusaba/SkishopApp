using System.Text.Json;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using InventoryManagementService.Services.Interfaces;

namespace InventoryManagementService.Services;

/// <summary>
/// Outbox パターンに基づくイベント発行サービスの実装クラス。
/// ドメインイベントを OutboxEvent テーブルに永続化し、BackgroundService が非同期で Kafka に配信する。
/// </summary>
/// <remarks>
/// <para>トピックルーティング: イベント種別ごとに専用トピック（inventory.products, inventory.levels, inventory.pricing）に振り分ける。</para>
/// <para>整合性保証: イベントは DB トランザクション内で Outbox テーブルに書き込まれるため、ビジネスデータとイベントの整合性が保証される。</para>
/// <para>H-5: AppDbContext 直接参照を IOutboxRepository 経由に変更。</para>
/// <para>H-25: CorrelationId を HttpContext から取得してイベントに付与。</para>
/// </remarks>
public class EventPublisherService(
    IOutboxRepository outboxRepository,
    IHttpContextAccessor httpContextAccessor,
    ILogger<EventPublisherService> logger) : IEventPublisherService
{
    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(
        string eventType, string topic, string aggregateId, string aggregateType,
        TEvent payload, CancellationToken ct = default) where TEvent : class
    {
        var correlationId = httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString();

        var outboxEvent = new OutboxEvent
        {
            EventType = eventType,
            Topic = topic,
            AggregateId = aggregateId,
            AggregateType = aggregateType,
            Payload = JsonSerializer.Serialize(payload),
            CorrelationId = correlationId
        };
        await outboxRepository.AddAsync(outboxEvent, ct);
        logger.LogInformation("Outbox イベント登録: EventType={EventType}, AggregateId={AggregateId}",
            eventType, aggregateId);
    }

    /// <inheritdoc />
    public Task PublishProductEventAsync(
        string eventType, string productId, object payload, CancellationToken ct = default)
        => PublishAsync(eventType, "inventory.products", productId, "Product", payload, ct);

    /// <inheritdoc />
    public Task PublishInventoryEventAsync(
        string eventType, string productId, object payload, CancellationToken ct = default)
        => PublishAsync(eventType, "inventory.levels", productId, "Inventory", payload, ct);

    /// <inheritdoc />
    public Task PublishPriceEventAsync(
        string eventType, string productId, object payload, CancellationToken ct = default)
        => PublishAsync(eventType, "inventory.pricing", productId, "Price", payload, ct);
}
