using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using PaymentCartService.Configurations;
using PaymentCartService.Infrastructure.Kafka.Events;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;

namespace PaymentCartService.Infrastructure.Kafka;

public class OrderCreatedConsumer(
    Func<string, IConsumer<string, string>> consumerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaSettings> kafkaOptions,
    ILogger<OrderCreatedConsumer> logger) : BackgroundService
{
    private readonly KafkaSettings _settings = kafkaOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var consumer = consumerFactory("order-created");
        consumer.Subscribe("order.created");
        logger.LogInformation("OrderCreatedConsumer 開始: Topic=order.created");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                using var correlationScope = CorrelationIdScope.Push();
                var @event = JsonSerializer.Deserialize<OrderCreatedEvent>(result.Message.Value);

                if (@event is not null)
                {
                    using var scope = scopeFactory.CreateScope();
                    var paymentRepository = scope.ServiceProvider
                        .GetRequiredService<IPaymentRepository>();

                    var existingPayment = await paymentRepository
                        .FindByOrderIdAsync(@event.OrderId, stoppingToken);

                    if (existingPayment is null)
                    {
                        var payment = new Payment
                        {
                            OrderId = @event.OrderId,
                            CustomerId = @event.CustomerId,
                            Amount = @event.TotalAmount,
                            CurrencyCode = @event.CurrencyCode,
                            Status = PaymentStatus.Pending,
                            PaymentMethod = "AWAITING_CHECKOUT"
                        };

                        await paymentRepository.AddAsync(payment, stoppingToken);
                        await paymentRepository.SaveChangesAsync(stoppingToken);

                        logger.LogInformation(
                            "注文受信・決済レコード作成: OrderId={OrderId}, PaymentId={PaymentId}",
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
                logger.LogError(ex, "OrderCreated イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(_settings.ConsumerErrorBackoffSeconds), stoppingToken);
            }
        }
    }
}
