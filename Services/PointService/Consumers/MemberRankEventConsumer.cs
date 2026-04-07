using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using PointService.Configurations;
using PointService.Events;
using StackExchange.Redis;

namespace PointService.Consumers;

public class MemberRankEventConsumer(
    IOptions<KafkaSettings> kafkaOptions,
    IConnectionMultiplexer redis,
    ILogger<MemberRankEventConsumer> logger) : BackgroundService
{
    private IConsumer<string, string>? _consumer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        var settings = kafkaOptions.Value;
        var config = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            GroupId = $"{settings.GroupId}-member-rank",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe("member_rank.updated");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await Task.Run(() => _consumer.Consume(stoppingToken), stoppingToken);
                var @event = JsonSerializer.Deserialize<MemberRankUpdatedEvent>(
                    result.Message.Value);

                if (@event is not null)
                {
                    var db = redis.GetDatabase();
                    await db.StringSetAsync(
                        $"point:tier:{@event.UserId}",
                        @event.CurrentRank,
                        TimeSpan.FromHours(24));

                    logger.LogInformation(
                        "ランク情報同期: UserId={UserId}, Rank={Rank}",
                        @event.UserId, @event.CurrentRank);
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
                logger.LogError(ex, "ランクイベント処理エラー: {Message}", ex.Message);
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
