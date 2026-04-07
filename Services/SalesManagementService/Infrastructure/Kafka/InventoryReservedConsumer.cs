using System.Text.Json;
using Confluent.Kafka;

namespace SalesManagementService.Infrastructure.Kafka;

public record InventoryReservedEvent(string OrderId, DateTimeOffset ReservedAt);

public class InventoryReservedConsumer(
    IConfiguration configuration,
    ILogger<InventoryReservedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("InventoryReservedConsumer を開始します");

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = "sales-inventory-reserved",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("inventory.reserved");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<InventoryReservedEvent>(result.Message.Value);

                if (@event is null)
                {
                    logger.LogWarning("inventory.reserved イベントのデシリアライズに失敗しました");
                    consumer.Commit(result);
                    continue;
                }

                // gRPC レスポンスで既に処理済みの場合が多いため、ログ記録のみ
                logger.LogInformation(
                    "在庫予約確認イベント受信: OrderId={OrderId}, ReservedAt={ReservedAt}",
                    @event.OrderId, @event.ReservedAt);

                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: Topic={Topic}", ex.ConsumerRecord?.Topic);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "inventory.reserved イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
        logger.LogInformation("InventoryReservedConsumer を停止しました");
    }
}
