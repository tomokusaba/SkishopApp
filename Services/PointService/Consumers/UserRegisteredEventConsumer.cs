using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using PointService.Configurations;
using PointService.Services.Interfaces;

namespace PointService.Consumers;

public record UserRegisteredEvent(string UserId, string CorrelationId);

public class UserRegisteredEventConsumer(
    IOptions<KafkaSettings> kafkaOptions,
    IServiceScopeFactory scopeFactory,
    ILogger<UserRegisteredEventConsumer> logger) : BackgroundService
{
    private IConsumer<string, string>? _consumer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        var settings = kafkaOptions.Value;
        var config = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            GroupId = $"{settings.GroupId}-user-registered",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe("user.registered");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await Task.Run(() => _consumer.Consume(stoppingToken), stoppingToken);
                var @event = JsonSerializer.Deserialize<UserRegisteredEvent>(
                    result.Message.Value);

                if (@event is not null)
                {
                    using var scope = scopeFactory.CreateScope();
                    var pointService = scope.ServiceProvider
                        .GetRequiredService<IPointService>();
                    await pointService.CreateAccountAsync(@event.UserId, stoppingToken);
                    logger.LogInformation(
                        "ユーザー登録: ポイントアカウント作成 UserId={UserId}",
                        @event.UserId);
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
                logger.LogError(ex, "ユーザー登録イベント処理エラー: {Message}", ex.Message);
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
