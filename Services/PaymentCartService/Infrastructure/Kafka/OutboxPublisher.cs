using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentCartService.Configurations;
using PaymentCartService.Infrastructure.Persistence;
using PaymentCartService.Models.Enums;

namespace PaymentCartService.Infrastructure.Kafka;

public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    IOptions<KafkaSettings> kafkaOptions,
    TimeProvider timeProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private readonly KafkaSettings _settings = kafkaOptions.Value;
    private TimeSpan _currentInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxPublisher 開始");
        _currentInterval = TimeSpan.FromMilliseconds(_settings.OutboxMinPollingIntervalMs * 5);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var correlationScope = CorrelationIdScope.Push();
                var publishedCount = await PublishPendingEventsAsync(stoppingToken);

                _currentInterval = publishedCount > 0
                    ? TimeSpan.FromMilliseconds(_settings.OutboxMinPollingIntervalMs)
                    : TimeSpan.FromTicks(Math.Min(
                        _currentInterval.Ticks * 2,
                        TimeSpan.FromMilliseconds(_settings.OutboxMaxPollingIntervalMs).Ticks));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "OutboxPublisher エラー: {Message}", ex.Message);
                _currentInterval = TimeSpan.FromMilliseconds(_settings.OutboxMaxPollingIntervalMs);
            }

            await Task.Delay(_currentInterval, stoppingToken);
        }
    }

    private async Task<int> PublishPendingEventsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var pendingEvents = await context.OutboxEvents
            .Where(e => e.Status == OutboxEventStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(_settings.OutboxBatchSize)
            .ToListAsync(ct);

        if (pendingEvents.Count == 0)
            return 0;

        foreach (var evt in pendingEvents)
        {
            try
            {
                evt.Status = OutboxEventStatus.Processing;
                await context.SaveChangesAsync(ct);

                await producer.ProduceAsync(evt.EventType, new Message<string, string>
                {
                    Key = evt.AggregateId,
                    Value = evt.Payload
                }, ct);

                evt.Status = OutboxEventStatus.Published;
                evt.PublishedAt = now;

                logger.LogInformation(
                    "Outbox イベント発行: EventType={EventType}, AggregateId={AggregateId}",
                    evt.EventType, evt.AggregateId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                evt.RetryCount++;
                evt.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;

                if (evt.RetryCount >= evt.MaxRetries)
                {
                    evt.Status = OutboxEventStatus.DeadLetter;
                    logger.LogError(ex,
                        "Outbox イベント Dead Letter: EventId={EventId}, RetryCount={RetryCount}",
                        evt.Id, evt.RetryCount);
                }
                else
                {
                    evt.Status = OutboxEventStatus.Pending;
                    logger.LogWarning(ex,
                        "Outbox イベントリトライ: EventId={EventId}, RetryCount={RetryCount}",
                        evt.Id, evt.RetryCount);
                }
            }
        }

        await context.SaveChangesAsync(ct);
        return pendingEvents.Count;
    }
}
