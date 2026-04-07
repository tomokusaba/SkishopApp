using System.Text.Json;
using Confluent.Kafka;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Events;
using InventoryManagementService.Services.Interfaces;
using Serilog.Context;

namespace InventoryManagementService.BackgroundServices;

/// <summary>
/// 注文キャンセルイベント（order.cancelled）の Kafka コンシューマー。
/// 引当済み在庫の解放（Release）を実行する BackgroundService。
/// </summary>
/// <param name="consumerFactory">Kafka コンシューマー生成ファクトリー</param>
/// <param name="scopeFactory">Scoped サービス取得用ファクトリー</param>
/// <param name="logger">ロガー</param>
/// <remarks>
/// - トピック: order.cancelled（コンシューマーグループ: inventory-order-cancelled）
/// - MessageDeduplicationService による冪等性保証
/// - Kafka ヘッダーから X-Correlation-Id を取得し、Serilog LogContext に Push
/// - ReservationId を使用して正確な引当解放を実行
/// - エラー時は 5 秒のバックオフ後にリトライ
/// </remarks>
public class OrderCancelledConsumer(
    Func<string, IConsumer<string, string>> consumerFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderCancelledConsumer> logger) : BackgroundService
{
    /// <summary>
    /// 注文キャンセルイベントの消費ループを実行する。
    /// </summary>
    /// <param name="stoppingToken">停止トークン</param>
    /// <remarks>
    /// 処理フロー:
    /// 1. Kafka トピック "order.cancelled" を購読し、メッセージを継続的に消費する
    /// 2. メッセージキーまたはパーティション/オフセットから一意の messageId を生成する
    /// 3. Kafka ヘッダーから X-Correlation-Id を取得し、Serilog LogContext に Push する
    /// 4. MessageDeduplicationService で処理済みチェックを行い、重複メッセージをスキップする
    /// 5. OrderCancelledEvent を JSON デシリアライズし、注文商品ごとに ReserveItemDto を構築する
    /// 6. IInventoryService.ReleaseAsync で OrderId と ReservationId を使用して引当済み在庫を解放する
    /// 7. 処理済みマーク登録後、Kafka オフセットをコミットする
    /// エラー時は ConsumeException と一般例外を分離して処理し、5 秒のバックオフ後にリトライする。
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var consumer = consumerFactory("inventory-order-cancelled");
        consumer.Subscribe("order.cancelled");
        logger.LogInformation("OrderCancelledConsumer started");

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

                    var @event = JsonSerializer.Deserialize<OrderCancelledEvent>(result.Message.Value);

                    if (@event is not null)
                    {
                        var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

                        var items = @event.Items
                            .Select(i => new ReserveItemDto(i.ProductId, i.Quantity))
                            .ToList();

                        await inventoryService.ReleaseAsync(
                            @event.OrderId, @event.ReservationId, items, stoppingToken);

                        logger.LogInformation(
                            "注文キャンセルによる在庫解放完了: OrderId={OrderId}, ReservationId={ReservationId}",
                            @event.OrderId, @event.ReservationId);
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
                logger.LogError(ex, "OrderCancelled イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
