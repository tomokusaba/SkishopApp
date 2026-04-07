using System.Text.Json;
using Confluent.Kafka;
using PaymentCartService.Infrastructure.Kafka.Events;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;

namespace PaymentCartService.Infrastructure.Kafka;

public class OrderCancelledConsumer(
    Func<string, IConsumer<string, string>> consumerFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderCancelledConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var consumer = consumerFactory("order-cancelled");
        consumer.Subscribe("order.cancelled");
        logger.LogInformation("OrderCancelledConsumer 開始: Topic=order.cancelled");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                using var correlationScope = CorrelationIdScope.Push();
                var @event = JsonSerializer.Deserialize<OrderCancelledEvent>(result.Message.Value);

                if (@event is not null)
                {
                    using var scope = scopeFactory.CreateScope();
                    var paymentRepository = scope.ServiceProvider
                        .GetRequiredService<IPaymentRepository>();

                    var payment = await paymentRepository
                        .FindByOrderIdAsync(@event.OrderId, stoppingToken);

                    if (payment is not null &&
                        payment.Status is PaymentStatus.Pending or PaymentStatus.Processing)
                    {
                        payment.MarkAsCancelled();
                        await paymentRepository.SaveChangesAsync(stoppingToken);

                        logger.LogInformation(
                            "注文キャンセルに伴う決済キャンセル: OrderId={OrderId}, PaymentId={PaymentId}",
                            @event.OrderId, payment.Id);
                    }
                }

                consumer.Commit(result);
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
