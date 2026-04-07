using PointService.Models;
using Confluent.Kafka;
using PointService.Repositories.Interfaces;

namespace PointService.BackgroundServices;

public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    TimeProvider timeProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan MinDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var currentDelay = MinDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxEventRepository>();

                var pendingEvents = await outboxRepository.FindPendingAsync(100, stoppingToken);

                if (pendingEvents.Count == 0)
                {
                    currentDelay = TimeSpan.FromTicks(
                        Math.Min(currentDelay.Ticks * 2, MaxDelay.Ticks));
                    await Task.Delay(currentDelay, stoppingToken);
                    continue;
                }

                currentDelay = MinDelay;

                foreach (var outboxEvent in pendingEvents)
                {
                    try
                    {
                        await producer.ProduceAsync(
                            outboxEvent.EventType,
                            new Message<string, string>
                            {
                                Key = outboxEvent.AggregateId,
                                Value = outboxEvent.Payload
                            }, stoppingToken);

                        outboxEvent.Status = OutboxStatuses.Published;
                        outboxEvent.PublishedAt = timeProvider.GetUtcNow().UtcDateTime;
                        logger.LogInformation(
                            "Outbox イベント発行成功: EventId={EventId}, Type={EventType}",
                            outboxEvent.Id, outboxEvent.EventType);
                    }
                    catch (ProduceException<string, string> ex)
                    {
                        outboxEvent.RetryCount++;
                        if (outboxEvent.RetryCount >= 5)
                            outboxEvent.Status = OutboxStatuses.Failed;
                        logger.LogError(ex,
                            "Outbox イベント発行失敗: EventId={EventId}, RetryCount={RetryCount}",
                            outboxEvent.Id, outboxEvent.RetryCount);
                    }
                }

                await outboxRepository.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "OutboxPublisher 処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
