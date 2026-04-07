using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Serilog.Context;
using UserManagementService.Events;
using UserManagementService.Services;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.BackgroundServices;

/// <summary>
/// Kafka <c>inventory.stock_updated</c> トピックの Consumer。在庫復活（previousQuantity=0 → newQuantity&gt;0）時に
/// ウィッシュリストの在庫通知対象を検索する。
/// </summary>
public class InventoryStockUpdatedConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<InventoryStockUpdatedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var consumer = consumerFactory.CreateConsumer("inventory-stock-updated");
        consumer.Subscribe("inventory.stock_updated");

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = await Task.Run(() => consumer.Consume(stoppingToken), stoppingToken);

                var correlationId = result.Message.Headers?
                    .FirstOrDefault(h => h.Key == "X-Correlation-Id")?
                    .GetValueBytes() is { } bytes
                    ? Encoding.UTF8.GetString(bytes)
                    : Guid.NewGuid().ToString();

                using (LogContext.PushProperty("CorrelationId", correlationId))
                {
                    var @event = JsonSerializer.Deserialize<InventoryStockUpdatedEvent>(result.Message.Value);
                    if (@event is null || string.IsNullOrWhiteSpace(@event.ProductId))
                    {
                        logger.LogWarning("無効なイベントデータをスキップ: {Topic}", "inventory.stock_updated");
                        consumer.Commit(result);
                        continue;
                    }

                    if (@event.PreviousQuantity == 0 && @event.NewQuantity > 0)
                    {
                        using var scope = scopeFactory.CreateScope();
                        var wishlistService = scope.ServiceProvider.GetRequiredService<IWishlistService>();
                        await wishlistService.ProcessRestockNotificationAsync(
                            @event.ProductId, stoppingToken);
                        logger.LogInformation("在庫復活通知処理: ProductId={ProductId}", @event.ProductId);
                    }
                    consumer.Commit(result);
                }
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}", ex.ConsumerRecord?.Topic);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "イベント処理エラー: {Message}", ex.Message);

                if (result is not null)
                {
                    try
                    {
                        using var dlScope = scopeFactory.CreateScope();
                        var dlProducer = dlScope.ServiceProvider.GetRequiredService<IProducer<string, string>>();
                        var dlMessage = new Message<string, string>
                        {
                            Key = result.Message.Key,
                            Value = result.Message.Value,
                            Headers = new Headers
                            {
                                { "X-Error-Message", Encoding.UTF8.GetBytes(ex.Message) },
                                { "X-Original-Topic", Encoding.UTF8.GetBytes("inventory.stock_updated") }
                            }
                        };
                        await dlProducer.ProduceAsync("inventory.stock_updated.dlt", dlMessage, stoppingToken);
                        consumer.Commit(result);
                    }
                    catch (Exception dlEx)
                    {
                        logger.LogError(dlEx, "DLT 転送失敗: {Topic}", "inventory.stock_updated.dlt");
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
