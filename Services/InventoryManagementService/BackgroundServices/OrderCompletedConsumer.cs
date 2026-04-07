using System.Text.Json;
using Confluent.Kafka;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Events;
using InventoryManagementService.Services.Interfaces;
using Serilog.Context;

namespace InventoryManagementService.BackgroundServices;

/// <summary>
/// 注文完了イベント（order.completed）の Kafka コンシューマー。
/// 引当済み在庫の確定（ConfirmReservation = 出庫確定）を実行する BackgroundService。
/// </summary>
/// <param name="consumerFactory">Kafka コンシューマー生成ファクトリー</param>
/// <param name="scopeFactory">Scoped サービス取得用ファクトリー</param>
/// <param name="logger">ロガー</param>
/// <remarks>
/// - トピック: order.completed（コンシューマーグループ: inventory-order-completed）
/// - MessageDeduplicationService による冪等性保証
/// - Kafka ヘッダーから X-Correlation-Id を取得し、Serilog LogContext に Push
/// - エラー時は 5 秒のバックオフ後にリトライ
/// </remarks>
public class OrderCompletedConsumer(
    Func<string, IConsumer<string, string>> consumerFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderCompletedConsumer> logger) : BackgroundService
{
    /// <summary>
    /// 注文完了イベントの消費ループを実行する。
    /// </summary>
    /// <param name="stoppingToken">停止トークン</param>
    /// <remarks>
    /// 処理フロー:
    /// 1. Kafka トピック "order.completed" を購読し、メッセージを継続的に消費する
    /// 2. メッセージキーまたはパーティション/オフセットから一意の messageId を生成する
    /// 3. Kafka ヘッダーから X-Correlation-Id を取得し、Serilog LogContext に Push する
    /// 4. MessageDeduplicationService で処理済みチェックを行い、重複メッセージをスキップする
    /// 5. OrderCompletedEvent を JSON デシリアライズし、注文商品ごとに ReserveItemDto を構築する
    /// 6. IInventoryService.ConfirmReservationAsync で引当済み在庫の出庫確定を実行する
    /// 7. 処理済みマーク登録後、Kafka オフセットをコミットする
    /// エラー時は ConsumeException と一般例外を分離して処理し、5 秒のバックオフ後にリトライする。
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var consumer = consumerFactory("inventory-order-completed");
        consumer.Subscribe("order.completed");
        logger.LogInformation("OrderCompletedConsumer started");

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

                    var @event = JsonSerializer.Deserialize<OrderCompletedEvent>(result.Message.Value);

                    if (@event is not null)
                    {
                        var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

                        var items = @event.Items
                            .Select(i => new ReserveItemDto(i.ProductId, i.Quantity))
                            .ToList();

                        await inventoryService.ConfirmReservationAsync(@event.OrderId, items, stoppingToken);

                        logger.LogInformation(
                            "注文完了による在庫確定処理完了: OrderId={OrderId}, Items={ItemCount}",
                            @event.OrderId, @event.Items.Count);
                    }

                    await dedup.MarkAsProcessedAsync(messageId, result.Topic, result.Partition.Value, result.Offset.Value, stoppingToken);
                    consumer.Commit(result);
                }
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}", ex.ConsumerRecord?.Topic);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "OrderCompleted イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
