using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Serilog.Context;
using UserManagementService.Events;
using UserManagementService.Services;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.BackgroundServices;

/// <summary>
/// Kafka <c>order.confirmed</c> トピックの Consumer。SalesManagementService からの注文確定イベントを受信し、
/// 購入金額を会員ランクに累積する。べき等性は <see cref="Services.MemberRankService"/> で保証する。
/// </summary>
public class OrderConfirmedConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderConfirmedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var consumer = consumerFactory.CreateConsumer("order-confirmed");
        consumer.Subscribe("order.confirmed");

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
                    var @event = JsonSerializer.Deserialize<OrderConfirmedEvent>(result.Message.Value);
                    if (@event is null || string.IsNullOrWhiteSpace(@event.UserId))
                    {
                        logger.LogWarning("無効なイベントデータをスキップ: {Topic}", "order.confirmed");
                        consumer.Commit(result);
                        continue;
                    }

                    {
                        using var scope = scopeFactory.CreateScope();
                        var memberRankService = scope.ServiceProvider.GetRequiredService<IMemberRankService>();
                        await memberRankService.AddPurchaseAmountAsync(
                            @event.UserId, @event.OrderId, @event.TotalAmount, stoppingToken);
                        logger.LogInformation("注文確定イベント処理: {UserId}, Amount={Amount}",
                            @event.UserId, @event.TotalAmount);
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
                                { "X-Original-Topic", Encoding.UTF8.GetBytes("order.confirmed") }
                            }
                        };
                        await dlProducer.ProduceAsync("order.confirmed.dlt", dlMessage, stoppingToken);
                        consumer.Commit(result);
                    }
                    catch (Exception dlEx)
                    {
                        logger.LogError(dlEx, "DLT 転送失敗: {Topic}", "order.confirmed.dlt");
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
