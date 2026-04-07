using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using SalesManagementService.Infrastructure.Persistence;
using SalesManagementService.Models.Enums;
using SalesManagementService.Services;

namespace SalesManagementService.Infrastructure.Kafka;

public record PaymentCompletedEvent(string OrderId, string TransactionId, decimal Amount, DateTimeOffset CompletedAt);

public class PaymentCompletedConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<PaymentCompletedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PaymentCompletedConsumer を開始します");

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = "sales-payment-completed",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("payment.completed");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<PaymentCompletedEvent>(result.Message.Value);

                if (@event is null)
                {
                    logger.LogWarning("payment.completed イベントのデシリアライズに失敗しました");
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

                order.PaymentStatus = "CAPTURED";
                OrderStateMachine.TransitionTo(order, OrderStatus.Confirmed);

                try
                {
                    await context.SaveChangesAsync(stoppingToken);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    logger.LogWarning(ex, "Concurrency conflict processing payment.completed event, will retry on next consume");
                    continue;
                }

                consumer.Commit(result);

                logger.LogInformation(
                    "決済完了処理成功: OrderId={OrderId}, TransactionId={TransactionId}, Amount={Amount}",
                    @event.OrderId, @event.TransactionId, @event.Amount);
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
                logger.LogError(ex, "payment.completed イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
        logger.LogInformation("PaymentCompletedConsumer を停止しました");
    }
}
