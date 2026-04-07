using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SalesManagementService.Configurations;
using SalesManagementService.Infrastructure.Persistence;

namespace SalesManagementService.Infrastructure.Outbox;

/// <summary>
/// Outbox パターンのイベント発行 BackgroundService。
/// PostgreSQL Advisory Lock を使用してプロセス間排他制御を行い、
/// 動的バックオフでポーリング間隔を調整する。
/// </summary>
public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    TimeProvider timeProvider,
    IOptions<OutboxSettings> outboxOptions,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private const string LockName = "outbox_publisher";
    private readonly OutboxSettings _settings = outboxOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxPublisher を開始します");

        var currentDelayMs = _settings.MinPollingIntervalMs;

        while (!stoppingToken.IsCancellationRequested)
        {
            var lockAcquired = false;

            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();

                // Advisory Lock を取得（プロセス間排他制御）
                // ADO.NET 直接使用で EF Core のオーバーヘッドを回避
                lockAcquired = await context.Database.TryAcquireAdvisoryLockAsync(LockName, stoppingToken);

                if (!lockAcquired)
                {
                    logger.LogDebug("Advisory Lock の取得に失敗。他のインスタンスが処理中です");
                    await Task.Delay(_settings.MaxPollingIntervalMs, stoppingToken);
                    continue;
                }

                var pendingEvents = await context.OutboxEvents
                    .Where(e => e.Status == "PENDING")
                    .OrderBy(e => e.CreatedAt)
                    .Take(_settings.BatchSize)
                    .ToListAsync(stoppingToken);

                if (pendingEvents.Count == 0)
                {
                    // イベントなし → バックオフを増加（指数的に最大まで）
                    currentDelayMs = Math.Min(currentDelayMs * 2, _settings.MaxPollingIntervalMs);
                }
                else
                {
                    // イベントあり → バックオフをリセット
                    currentDelayMs = _settings.MinPollingIntervalMs;

                    foreach (var outboxEvent in pendingEvents)
                    {
                        try
                        {
                            var message = new Message<string, string>
                            {
                                Key = outboxEvent.AggregateId,
                                Value = outboxEvent.Payload
                            };

                            await producer.ProduceAsync(outboxEvent.EventType, message, stoppingToken);

                            outboxEvent.Status = "PUBLISHED";
                            outboxEvent.PublishedAt = timeProvider.GetUtcNow();

                            logger.LogInformation(
                                "Outbox イベント発行成功: EventId={EventId}, EventType={EventType}, AggregateId={AggregateId}",
                                outboxEvent.Id, outboxEvent.EventType, outboxEvent.AggregateId);
                        }
                        catch (ProduceException<string, string> ex)
                        {
                            outboxEvent.RetryCount++;
                            outboxEvent.LastError = ex.Error.Reason;

                            if (outboxEvent.RetryCount >= _settings.MaxRetries)
                            {
                                outboxEvent.Status = "FAILED";
                                logger.LogError(ex,
                                    "Outbox イベント発行失敗（最大リトライ到達）: EventId={EventId}, RetryCount={RetryCount}",
                                    outboxEvent.Id, outboxEvent.RetryCount);
                            }
                            else
                            {
                                logger.LogWarning(ex,
                                    "Outbox イベント発行リトライ: EventId={EventId}, RetryCount={RetryCount}, Error={Error}",
                                    outboxEvent.Id, outboxEvent.RetryCount, ex.Error.Reason);
                            }
                        }
                    }

                    await context.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutboxPublisher で予期しないエラーが発生: {Message}", ex.Message);
                currentDelayMs = Math.Min(currentDelayMs * 2, _settings.MaxPollingIntervalMs);
            }
            finally
            {
                if (lockAcquired)
                {
                    try
                    {
                        using var unlockScope = scopeFactory.CreateScope();
                        var unlockContext = unlockScope.ServiceProvider.GetRequiredService<SalesDbContext>();
                        await unlockContext.Database.ReleaseAdvisoryLockAsync(LockName, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Advisory Lock の解放に失敗: {Message}", ex.Message);
                    }
                }
            }

            await Task.Delay(currentDelayMs, stoppingToken);
        }

        logger.LogInformation("OutboxPublisher を停止しました");
    }
}
