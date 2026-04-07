using System.Text.Json;
using SalesManagementService.Infrastructure.Persistence;
using SalesManagementService.Models;
using SalesManagementService.Services.Interfaces;

namespace SalesManagementService.Infrastructure.Outbox;

public class OutboxWriter(
    SalesDbContext context,
    ILogger<OutboxWriter> logger) : IOutboxWriter
{
    public Task WriteAsync<T>(string eventType, string aggregateId, T payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(aggregateId);
        ArgumentNullException.ThrowIfNull(payload);

        var outboxEvent = new OutboxEvent
        {
            EventType = eventType,
            AggregateId = aggregateId,
            Payload = JsonSerializer.Serialize(payload),
            Status = "PENDING"
        };

        context.OutboxEvents.Add(outboxEvent);

        logger.LogInformation(
            "Outbox イベント追加: EventType={EventType}, AggregateId={AggregateId}, EventId={EventId}",
            eventType, aggregateId, outboxEvent.Id);

        return Task.CompletedTask;
    }
}
