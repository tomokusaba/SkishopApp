using System.Text.Json;
using Confluent.Kafka;

namespace SalesManagementService.Infrastructure.Kafka;

public record InventoryReleasedEvent(string OrderId, DateTimeOffset ReleasedAt);

public class InventoryReleasedConsumer(
    IConfiguration configuration,
    ILogger<InventoryReleasedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("InventoryReleasedConsumer を開始します");

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = "sales-inventory-released",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("inventory.released");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<InventoryReleasedEvent>(result.Message.Value);

                if (@event is null)
                {
                    logger.LogWarning("inventory.released イベントのデシリアライズに失敗しました");
                    consumer.Commit(result);
                    continue;
                }

                // 補償トランザクション（在庫解放）の確認ログ
                logger.LogInformation(
                    "在庫解放確認イベント受信（補償完了）: OrderId={OrderId}, ReleasedAt={ReleasedAt}",
                    @event.OrderId, @event.ReleasedAt);

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
                logger.LogError(ex, "inventory.released イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
        logger.LogInformation("InventoryReleasedConsumer を停止しました");
    }
}
