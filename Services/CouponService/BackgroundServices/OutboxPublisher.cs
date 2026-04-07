using Confluent.Kafka;
using CouponService.Models;
using CouponService.Repositories.Interfaces;

namespace CouponService.BackgroundServices;

public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    TimeProvider timeProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan MinPollingInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaxPollingInterval = TimeSpan.FromSeconds(5);
    private const long AdvisoryLockId = 100001;
    private TimeSpan _currentInterval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxPublisher 開始");

        // 起動時に PROCESSING ステータスのリカバリ (M-25)
        try
        {
            using var initScope = scopeFactory.CreateScope();
            var initRepo = initScope.ServiceProvider.GetRequiredService<IOutboxEventRepository>();
            await initRepo.RecoverProcessingEventsAsync(stoppingToken);
            logger.LogInformation("PROCESSING ステータスのリカバリ完了");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "PROCESSING リカバリでエラーが発生しました");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxEventsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox 処理でエラーが発生しました");
                _currentInterval = MaxPollingInterval;
            }

            await Task.Delay(_currentInterval, stoppingToken);
        }

        logger.LogInformation("OutboxPublisher 終了");
    }

    private async Task ProcessOutboxEventsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxEventRepository>();

        // Advisory Lock でリーダー選出（複数インスタンスの二重発行防止）
        var lockAcquired = false;
        try
        {
            lockAcquired = await outboxRepository.TryAcquireAdvisoryLockAsync(AdvisoryLockId, ct);

            if (!lockAcquired)
            {
                _currentInterval = MaxPollingInterval;
                return;
            }

            // PENDING イベントを取得し PROCESSING に遷移
            var pendingEvents = await outboxRepository.GetPendingAndMarkProcessingAsync(100, ct);

            if (pendingEvents.Count == 0)
            {
                _currentInterval = TimeSpan.FromTicks(
                    Math.Min(_currentInterval.Ticks * 2, MaxPollingInterval.Ticks));
                return;
            }

            foreach (var evt in pendingEvents)
            {
                try
                {
                    await producer.ProduceAsync(evt.EventType,
                        new Message<string, string>
                        {
                            Key = evt.AggregateId,
                            Value = evt.Payload
                        }, ct);

                    evt.Status = OutboxEventStatus.Published;
                    evt.PublishedAt = timeProvider.GetUtcNow();
                }
                catch (ProduceException<string, string> ex)
                {
                    evt.RetryCount++;
                    evt.LastError = ex.Message.Length > 2000
                        ? ex.Message[..2000] : ex.Message;
                    evt.Status = evt.RetryCount >= evt.MaxRetries
                        ? OutboxEventStatus.Failed
                        : OutboxEventStatus.Pending;

                    logger.LogError(ex,
                        "Outbox publish 失敗: EventId={EventId}, Retry={RetryCount}/{MaxRetries}",
                        evt.Id, evt.RetryCount, evt.MaxRetries);
                }
            }

            await outboxRepository.UpdateBatchAsync(ct);
            _currentInterval = MinPollingInterval;
        }
        finally
        {
            if (lockAcquired)
            {
                await outboxRepository.ReleaseAdvisoryLockAsync(AdvisoryLockId, ct);
            }
        }
    }
}
