using Confluent.Kafka;
using MailSendService.Models;
using MailSendService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog.Context;

namespace MailSendService.Consumers;

/// <summary>
/// Outbox パターンを実装するバックグラウンドサービス。
/// 未発行の <see cref="Models.OutboxEvent"/> を Kafka "mail-events" トピックへ発行する。
/// </summary>
/// <remarks>
/// <para>動的バックオフ戦略を採用: 未発行イベントが存在する場合は 100ms（<see cref="MinDelay"/>）、
/// 存在しない場合は 5 秒（<see cref="MaxDelay"/>）の待機を行う。</para>
/// <para>PENDING ステータスのイベントを最大 <see cref="MaxBatchSize"/> 件ずつ取得して発行し、
/// 発行成功時は PUBLISHED、失敗時は FAILED にステータスを更新する。</para>
/// <para>FAILED ステータスのイベントは <see cref="FailedRetryCooldown"/>（5 分）経過後に
/// PENDING へ戻して再発行を試みる。P1-13: UpdatedAt を基準にクールダウンを判定する。</para>
/// <para>P1-16: CorrelationId を LogContext に設定する。</para>
/// <para>P1-19: バッチ処理の最適化。</para>
/// </remarks>
/// <param name="scopeFactory">Scoped サービス解決用のスコープファクトリ。</param>
/// <param name="producer">Kafka プロデューサーインスタンス。</param>
/// <param name="timeProvider">テスト可能な時刻プロバイダー。</param>
/// <param name="logger">ロガー。</param>
public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    TimeProvider timeProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    /// <summary>1 回のポーリングで取得する最大イベント数。</summary>
    private const int MaxBatchSize = 50;  // P1-19: 20 → 50 に増加
    /// <summary>イベント検出時の最小ポーリング間隔（100ms）。</summary>
    private static readonly TimeSpan MinDelay = TimeSpan.FromMilliseconds(100);
    /// <summary>イベント未検出時の最大ポーリング間隔（5 秒）。</summary>
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(5);
    /// <summary>FAILED イベントを PENDING に戻すまでのクールダウン期間（5 分）。</summary>
    private static readonly TimeSpan FailedRetryCooldown = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Outbox テーブルから未発行イベントをポーリングし、Kafka へ発行するメインループを実行する。
    /// </summary>
    /// <param name="stoppingToken">サービス停止要求を通知するキャンセルトークン。</param>
    /// <remarks>
    /// PENDING イベントが見つかった場合は各イベントを Kafka へ発行し、ステータスを更新する。
    /// PENDING イベントが無い場合は FAILED イベントのクールダウン経過確認を行い、
    /// 条件を満たすイベントを PENDING に戻す。
    /// P1-13: CreatedAt ではなく UpdatedAt を基準にクールダウンを判定する。
    /// P1-16: CorrelationId を LogContext に設定する。
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // P1-16: BackgroundService 用の CorrelationId を設定
        using var _ = LogContext.PushProperty("CorrelationId", $"outbox-{Guid.NewGuid():N}");
        logger.LogInformation("OutboxPublisher started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxEventRepository>();

                var pendingEvents = await outboxRepository.FindPendingAsync(MaxBatchSize, stoppingToken);

                if (pendingEvents.Count == 0)
                {
                    var failedEvents = await outboxRepository.FindFailedAsync(MaxBatchSize, stoppingToken);
                    var now = timeProvider.GetUtcNow();
                    // P1-13: CreatedAt → UpdatedAt に変更（リトライバックオフのため）
                    var retryableEvents = failedEvents
                        .Where(e => e.UpdatedAt.Add(FailedRetryCooldown) <= now)
                        .ToList();

                    if (retryableEvents.Count == 0)
                    {
                        await Task.Delay(MaxDelay, stoppingToken);
                        continue;
                    }

                    foreach (var evt in retryableEvents)
                    {
                        evt.ResetToPending();  // P0-8: DDD ドメインメソッド使用
                    }
                    try
                    {
                        await outboxRepository.SaveChangesAsync(stoppingToken);
                        logger.LogInformation("Reset {Count} FAILED events to PENDING", retryableEvents.Count);
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        logger.LogWarning(ex, "Concurrency conflict resetting FAILED events to PENDING");
                    }
                    continue;
                }

                // P1-19: バッチ内での成功/失敗カウント
                var successCount = 0;
                var failureCount = 0;

                foreach (var evt in pendingEvents)
                {
                    try
                    {
                        await producer.ProduceAsync("mail-events",
                            new Message<string, string> { Key = evt.Id, Value = evt.Payload },
                            stoppingToken);
                        evt.MarkAsPublished(timeProvider.GetUtcNow());  // P0-8: DDD ドメインメソッド使用
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Outbox publish failed: {EventId}", evt.Id);
                        evt.MarkAsFailed();  // P0-8: DDD ドメインメソッド使用
                        failureCount++;
                    }
                }

                try
                {
                    await outboxRepository.SaveChangesAsync(stoppingToken);
                    if (successCount > 0 || failureCount > 0)
                    {
                        logger.LogInformation(
                            "Outbox batch processed: {SuccessCount} published, {FailureCount} failed",
                            successCount, failureCount);
                    }
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    logger.LogWarning(ex, "Concurrency conflict saving outbox event status updates");
                }
                await Task.Delay(MinDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutboxPublisher error: {Message}", ex.Message);
                await Task.Delay(MaxDelay, stoppingToken);
            }
        }

        logger.LogInformation("OutboxPublisher stopped.");
    }
}
