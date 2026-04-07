using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using SalesManagementService.Infrastructure.Persistence;
using SalesManagementService.Models.Enums;
using SalesManagementService.Services;

namespace SalesManagementService.Infrastructure.Kafka;

public record PaymentFailedEvent(string OrderId, string Reason, DateTimeOffset FailedAt);

public class PaymentFailedConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<PaymentFailedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PaymentFailedConsumer を開始します");

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = "sales-payment-failed",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("payment.failed");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<PaymentFailedEvent>(result.Message.Value);

                if (@event is null)
                {
                    logger.LogWarning("payment.failed イベントのデシリアライズに失敗しました");
                    consumer.Commit(result);
                    continue;
                }

                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();

                var order = await context.Orders
                    .FirstOrDefaultAsync(o => o.Id == @event.OrderId, stoppingToken);

                if (order is null)
                {
                    logger.LogWarning("注文が見つかりません: OrderId={OrderId}", @event.OrderId);
                    consumer.Commit(result);
                    continue;
                }

                order.PaymentStatus = "FAILED";
                OrderStateMachine.TransitionTo(order, OrderStatus.PaymentFailed);

                try
                {
                    await context.SaveChangesAsync(stoppingToken);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    logger.LogWarning(ex, "Concurrency conflict processing payment.failed event, will retry on next consume");
                    continue;
                }

                consumer.Commit(result);

                logger.LogInformation(
                    "決済失敗処理完了: OrderId={OrderId}, Reason={Reason}",
                    @event.OrderId, @event.Reason);
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
                logger.LogError(ex, "payment.failed イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
        logger.LogInformation("PaymentFailedConsumer を停止しました");
    }
}
