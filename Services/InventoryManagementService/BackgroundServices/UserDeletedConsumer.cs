using Confluent.Kafka;
using InventoryManagementService.Services.Interfaces;
using Serilog.Context;

namespace InventoryManagementService.BackgroundServices;

/// <summary>
/// ユーザー削除イベント（user.deleted）の Kafka コンシューマー。
/// GDPR DSR（データ主体の権利）対応として、削除ユーザーのレビューを匿名化する BackgroundService。
/// </summary>
/// <param name="consumerFactory">Kafka コンシューマー生成ファクトリー</param>
/// <param name="scopeFactory">Scoped サービス取得用ファクトリー</param>
/// <param name="logger">ロガー</param>
/// <remarks>
/// - トピック: user.deleted（コンシューマーグループ: inventory-user-deleted）
/// - MessageDeduplicationService による冪等性保証
/// - メッセージキーから userId を取得。空の場合はスキップ
/// - IInventoryService.AnonymizeUserReviewsAsync で該当ユーザーのレビューを匿名化
/// - エラー時は 5 秒のバックオフ後にリトライ
/// </remarks>
public class UserDeletedConsumer(
    Func<string, IConsumer<string, string>> consumerFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<UserDeletedConsumer> logger) : BackgroundService
{
    /// <summary>
    /// ユーザー削除イベントの消費ループを実行する。
    /// </summary>
    /// <param name="stoppingToken">停止トークン</param>
    /// <remarks>
    /// 処理フロー:
    /// 1. Kafka トピック "user.deleted" を購読し、メッセージを継続的に消費する
    /// 2. メッセージキーまたはパーティション/オフセットから一意の messageId を生成する
    /// 3. Kafka ヘッダーから X-Correlation-Id を取得し、Serilog LogContext に Push する
    /// 4. MessageDeduplicationService で処理済みチェックを行い、重複メッセージをスキップする
    /// 5. メッセージキーから userId を取得する。空の場合は警告ログを出力しスキップする
    /// 6. IInventoryService.AnonymizeUserReviewsAsync で該当ユーザーのレビューを GDPR 準拠で匿名化する
    /// 7. 処理済みマーク登録後、Kafka オフセットをコミットする
    /// エラー時は ConsumeException と一般例外を分離して処理し、5 秒のバックオフ後にリトライする。
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var consumer = consumerFactory("inventory-user-deleted");
        consumer.Subscribe("user.deleted");
        logger.LogInformation("UserDeletedConsumer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var messageId = result.Message.Key ?? $"{result.Topic}-{result.Partition.Value}-{result.Offset.Value}";

                var correlationId = result.Message.Headers?
                    .FirstOrDefault(h => h.Key == "X-Correlation-Id")
                    ?.GetValueBytes() is { } bytes
                    ? System.Text.Encoding.UTF8.GetString(bytes)
                    : Guid.NewGuid().ToString();

                using (LogContext.PushProperty("CorrelationId", correlationId))
                {
                    using var scope = scopeFactory.CreateScope();
                    var dedup = scope.ServiceProvider.GetRequiredService<IMessageDeduplicationService>();

                    if (await dedup.IsProcessedAsync(messageId, stoppingToken))
                    {
                        logger.LogInformation("重複メッセージスキップ: MessageId={MessageId}", messageId);
                        consumer.Commit(result);
                        continue;
                    }

                    var userId = result.Message.Key;

                    if (string.IsNullOrEmpty(userId))
                    {
                        logger.LogWarning("UserDeletedConsumer: userId が空のメッセージを受信しました");
                        consumer.Commit(result);
                        continue;
                    }

                    var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();
                    await inventoryService.AnonymizeUserReviewsAsync(userId, stoppingToken);

                    await dedup.MarkAsProcessedAsync(messageId, result.Topic, result.Partition.Value, result.Offset.Value, stoppingToken);
                    consumer.Commit(result);

                    logger.LogInformation(
                        "GDPR DSR 処理完了: UserId={UserId}",
                        userId);
                }
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}", ex.ConsumerRecord?.Topic);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "ユーザー削除イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
