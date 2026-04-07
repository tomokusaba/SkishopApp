using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using PointService.Configurations;
using PointService.Services.Interfaces;

namespace PointService.Consumers;

public record OrderCreatedEvent(
    string OrderId, string UserId, decimal TotalAmount, string CorrelationId);

public record OrderCancelledEvent(
    string OrderId, string UserId, string CorrelationId);

public class OrderEventConsumer(
    IOptions<KafkaSettings> kafkaOptions,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderEventConsumer> logger) : BackgroundService
{
    private IConsumer<string, string>? _consumer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        var settings = kafkaOptions.Value;
        var config = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            GroupId = $"{settings.GroupId}-order",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe(["order.created", "order.cancelled"]);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await Task.Run(() => _consumer.Consume(stoppingToken), stoppingToken);
                var topic = result.Topic;

                using var scope = scopeFactory.CreateScope();
                var pointService = scope.ServiceProvider.GetRequiredService<IPointService>();

                if (topic == "order.created")
                {
                    var @event = JsonSerializer.Deserialize<OrderCreatedEvent>(
                        result.Message.Value);
                    if (@event is not null)
                    {
                        logger.LogInformation(
                            "注文作成イベント受信: OrderId={OrderId}, UserId={UserId}, Amount={Amount}",
                            @event.OrderId, @event.UserId, @event.TotalAmount);
                    }
                }
                else if (topic == "order.cancelled")
                {
                    var @event = JsonSerializer.Deserialize<OrderCancelledEvent>(
                        result.Message.Value);
                    if (@event is not null)
                    {
                        await pointService.ReleasePointsAsync(
                            @event.UserId, @event.OrderId, stoppingToken);
                        logger.LogInformation(
                            "注文キャンセル: ポイント返却完了 OrderId={OrderId}",
                            @event.OrderId);
                    }
                }

                _consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}",
                    ex.ConsumerRecord?.Topic);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "注文イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _consumer?.Close();
        _consumer?.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
