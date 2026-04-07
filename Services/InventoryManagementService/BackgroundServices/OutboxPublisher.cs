using System.Text;
using Confluent.Kafka;
using InventoryManagementService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementService.BackgroundServices;

/// <summary>
/// Outbox パターンのイベント発行 BackgroundService。
/// outbox_events テーブルの PENDING/FAILED イベントをポーリングし、Kafka に発行する。
/// </summary>
/// <param name="scopeFactory">Scoped サービス取得用ファクトリー</param>
/// <param name="producer">Kafka プロデューサー</param>
/// <param name="logger">ロガー</param>
/// <remarks>
/// - pg_try_advisory_lock(12345) で複数インスタンス間の排他制御を行う
/// - 動的バックオフ: イベントがある場合は 100ms、ない場合は最大 5000ms まで倍増
/// - 各イベントに MaxRetries を設定し、超過時は DEAD_LETTER ステータスに移行
/// - イベント処理順序: CreatedAt 昇順、1 回のポーリングで最大 50 件処理
/// - C-2: バッチ SaveChanges 最適化 — SaveChanges はバッチ開始/終了時の計 2 回に削減
/// </remarks>
public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private const int MinDelayMs = 100;
    private const int MaxDelayMs = 5000;
    private int _currentDelayMs = MinDelayMs;

    /// <summary>
    /// Outbox イベントのポーリングと Kafka 発行ループを実行する。
    /// </summary>
    /// <param name="stoppingToken">停止トークン</param>
    /// <remarks>
    /// 処理フロー:
    /// 1. pg_try_advisory_lock(12345) で PostgreSQL アドバイザリロックを取得し、複数インスタンス間の排他制御を行う
    /// 2. outbox_events テーブルから PENDING または FAILED（リトライ上限未達）のイベントを CreatedAt 昇順で最大 50 件取得する
    /// 3. 全イベントのステータスを PROCESSING に一括更新（1 回目の SaveChanges）
    /// 4. 各イベントを Kafka ProduceAsync でメッセージを発行する
    /// 5. 発行成功時はステータスを PUBLISHED に更新し、PublishedAt を記録する
    /// 6. 発行失敗時はステータスを FAILED に更新し、RetryCount をインクリメントする。MaxRetries 超過時は DEAD_LETTER に移行する
    /// 7. 全イベントのステータス変更を一括保存（2 回目の SaveChanges）
    /// 8. 動的バックオフ: 処理対象イベントがある場合は 100ms、ない場合は現在の待機時間を倍増（最大 5000ms）
    /// 9. 最後に pg_advisory_unlock(12345) でロックを解放する
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxPublisher started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await using var connection = context.Database.GetDbConnection();
                await connection.OpenAsync(stoppingToken);
                await using var lockCmd = connection.CreateCommand();
                lockCmd.CommandText = "SELECT pg_try_advisory_lock(12345)";
                var acquired = (bool)(await lockCmd.ExecuteScalarAsync(stoppingToken) ?? false);

                if (!acquired)
                {
                    await Task.Delay(_currentDelayMs, stoppingToken);
                    continue;
                }

                try
                {
                    var events = await context.OutboxEvents
                        .Where(e => e.Status == "PENDING"
                            || (e.Status == "FAILED" && e.RetryCount < e.MaxRetries))
                        .OrderBy(e => e.CreatedAt)
                        .Take(50)
                        .ToListAsync(stoppingToken);

                    if (events.Count > 0)
                    {
                        // C-2: ステータスを PROCESSING に一括更新（1 回目の SaveChanges）
                        foreach (var evt in events)
                        {
                            evt.Status = "PROCESSING";
                        }
                        await context.SaveChangesAsync(stoppingToken);

                        // 各イベントを Kafka に発行
                        foreach (var evt in events)
                        {
                            try
                            {
                                var message = new Message<string, string>
                                {
                                    Key = evt.AggregateId,
                                    Value = evt.Payload,
                                    Headers = new Headers
                                    {
                                        { "event-type", Encoding.UTF8.GetBytes(evt.EventType) },
                                        // H-25: CorrelationId を Kafka ヘッダーに付与
                                        { "X-Correlation-Id", Encoding.UTF8.GetBytes(evt.CorrelationId ?? Guid.NewGuid().ToString()) }
                                    }
                                };
                                await producer.ProduceAsync(evt.Topic, message, stoppingToken);

                                evt.Status = "PUBLISHED";
                                evt.PublishedAt = DateTimeOffset.UtcNow;

                                logger.LogInformation(
                                    "Outbox イベント発行: {EventType}, AggregateId={AggregateId}",
                                    evt.EventType, evt.AggregateId);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                evt.Status = "FAILED";
                                evt.RetryCount++;
                                evt.LastError = ex.Message[..Math.Min(ex.Message.Length, 2000)];

                                if (evt.RetryCount >= evt.MaxRetries)
                                    evt.Status = "DEAD_LETTER";

                                logger.LogError(ex, "Outbox イベント発行失敗: {EventId}", evt.Id);
                            }
                        }

                        // C-2: 全イベントのステータスを一括保存（2 回目の SaveChanges）
                        await context.SaveChangesAsync(stoppingToken);

                        _currentDelayMs = MinDelayMs;
                    }
                    else
                    {
                        _currentDelayMs = Math.Min(_currentDelayMs * 2, MaxDelayMs);
                    }
                }
                finally
                {
                    // H-15: finally 内では CancellationToken.None を使用（キャンセル時もロック解放を保証）
                    await using var unlockCmd = connection.CreateCommand();
                    unlockCmd.CommandText = "SELECT pg_advisory_unlock(12345)";
                    await unlockCmd.ExecuteScalarAsync(CancellationToken.None);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "OutboxPublisher エラー: {Message}", ex.Message);
                _currentDelayMs = MaxDelayMs;
            }

            await Task.Delay(_currentDelayMs, stoppingToken);
        }
    }
}
