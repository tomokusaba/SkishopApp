using Confluent.Kafka;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;

namespace UserManagementService.BackgroundServices;

/// <summary>
/// Outbox パターンのバックグラウンドパブリッシャー。PENDING の OutboxEvent をバッチ取得し、
/// Kafka に発行する。動的バックオフ（100ms〜5s）でポーリング間隔を調整する。
/// PostgreSQL Advisory Lock で複数インスタンスの排他制御を行う。
/// </summary>
public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    TimeProvider timeProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    /// <summary>動的バックオフの最小ディレイ。</summary>
    private static readonly TimeSpan MinDelay = TimeSpan.FromMilliseconds(100);
    /// <summary>動的バックオフの最大ディレイ。</summary>
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

                var lockAcquired = await outboxRepository.TryAcquirePublishLockAsync(stoppingToken);

                if (!lockAcquired)
                {
                    await Task.Delay(MaxDelay, stoppingToken);
                    continue;
                }

                try
                {
                    var events = await outboxRepository.FindPendingAsync(100, stoppingToken);

                    if (events.Count == 0)
                    {
                        currentDelay = currentDelay * 2 > MaxDelay ? MaxDelay : currentDelay * 2;
                        await Task.Delay(currentDelay, stoppingToken);
                        continue;
                    }

                    currentDelay = MinDelay;

                    foreach (var @event in events)
                    {
                        var message = new Message<string, string>
                        {
                            Key = @event.AggregateId,
                            Value = @event.Payload
                        };
                        await producer.ProduceAsync(@event.EventType, message, stoppingToken);
                        @event.Status = OutboxEventStatus.Published;
                        @event.PublishedAt = timeProvider.GetUtcNow();
                    }

                    await outboxRepository.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Outbox: {Count} 件のイベントを発行しました", events.Count);
                }
                finally
                {
                    await outboxRepository.ReleasePublishLockAsync(CancellationToken.None);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox パブリッシャーでエラーが発生しました: {Message}", ex.Message);
                await Task.Delay(MaxDelay, stoppingToken);
            }
        }
    }
}
