using System.Text.Json;
using Confluent.Kafka;
using CouponService.DTOs.Requests;
using CouponService.Infrastructure.Persistence;
using CouponService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CouponService.Consumers;

public class OrderEventConsumer(
    IConsumer<string, string> consumer,
    IProducer<string, string> producer,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderEventConsumer> logger) : BackgroundService
{
    private const string DeadLetterTopicSuffix = ".dlq";
    private const int MaxProcessingRetries = 3;
    private static readonly TimeSpan MinBackoff = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
    private TimeSpan _currentBackoff = MinBackoff;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OrderEventConsumer 開始");
        consumer.Subscribe(["order.cancelled", "user.deleted"]);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null) continue;

                using var scope = scopeFactory.CreateScope();

                switch (result.Topic)
                {
                    case "order.cancelled":
                        await HandleOrderCancelledAsync(result.Message.Value, scope, stoppingToken);
                        break;
                    case "user.deleted":
                        await HandleUserDeletedAsync(result.Message.Value, scope, stoppingToken);
                        break;
                }

                consumer.Commit(result);
                _currentBackoff = MinBackoff;
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}",
                    ex.ConsumerRecord?.Topic);
                await Task.Delay(_currentBackoff, stoppingToken);
                _currentBackoff = TimeSpan.FromTicks(
                    Math.Min(_currentBackoff.Ticks * 2, MaxBackoff.Ticks));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "イベント処理エラー: {Message}", ex.Message);
                if (result?.Message is not null)
                    await ForwardToDeadLetterAsync(result.Topic, result.Message, ex, stoppingToken);
                await Task.Delay(_currentBackoff, stoppingToken);
                _currentBackoff = TimeSpan.FromTicks(
                    Math.Min(_currentBackoff.Ticks * 2, MaxBackoff.Ticks));
            }
        }

        logger.LogInformation("OrderEventConsumer 終了");
    }

    private async Task ForwardToDeadLetterAsync(
        string originalTopic, Message<string, string> message, Exception ex, CancellationToken ct)
    {
        try
        {
            var dltMessage = new Message<string, string>
            {
                Key = message.Key,
                Value = message.Value,
                Headers = new Headers
                {
                    { "x-original-topic", System.Text.Encoding.UTF8.GetBytes(originalTopic) },
                    { "x-exception-type", System.Text.Encoding.UTF8.GetBytes(ex.GetType().Name) },
                    { "x-exception-message", System.Text.Encoding.UTF8.GetBytes(ex.Message) },
                    { "x-failed-at", System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToString("o")) }
                }
            };

            await producer.ProduceAsync($"{originalTopic}{DeadLetterTopicSuffix}", dltMessage, ct);
            logger.LogWarning("DLT 転送完了: Topic={DltTopic}, Key={Key}",
                $"{originalTopic}{DeadLetterTopicSuffix}", message.Key);
        }
        catch (Exception dltEx)
        {
            logger.LogError(dltEx, "DLT 転送失敗: OriginalTopic={Topic}", originalTopic);
        }
    }

    private async Task HandleOrderCancelledAsync(
        string payload, IServiceScope scope, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payload)) return;

        var cancelEvent = JsonSerializer.Deserialize<OrderCancelledEvent>(payload);
        if (cancelEvent?.CouponId is null) return;

        var couponService = scope.ServiceProvider.GetRequiredService<ICouponService>();
        await couponService.ReleaseCouponAsync(
            new ReleaseCouponRequest(cancelEvent.CouponId, cancelEvent.OrderId), ct);

        logger.LogInformation(
            "注文キャンセルによるクーポンリリース: OrderId={OrderId}", cancelEvent.OrderId);
    }

    private async Task HandleUserDeletedAsync(
        string payload, IServiceScope scope, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payload)) return;

        var deleteEvent = JsonSerializer.Deserialize<UserDeletedEvent>(payload);
        if (deleteEvent is null) return;

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // CouponUsage / UserCoupon のユーザーID を仮名化
        var anonymizedId = $"deleted-{Guid.NewGuid():N}";
        await context.CouponUsages
            .Where(u => u.UserId == deleteEvent.UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.UserId, anonymizedId), ct);

        await context.UserCoupons
            .Where(uc => uc.UserId == deleteEvent.UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(uc => uc.UserId, anonymizedId), ct);

        logger.LogInformation("ユーザー仮名化完了: 元UserId のデータを匿名化しました");
    }
}
