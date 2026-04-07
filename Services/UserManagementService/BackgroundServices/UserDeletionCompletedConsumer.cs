using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Serilog.Context;
using UserManagementService.Events;
using UserManagementService.Services;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.BackgroundServices;

/// <summary>
/// Kafka <c>user.deletion.completed</c> トピックの Consumer。他マイクロサービスからの
/// 削除完了/失敗通知を受信し、DeletionRequest のステータスを更新する。
/// </summary>
public class UserDeletionCompletedConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<UserDeletionCompletedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var consumer = consumerFactory.CreateConsumer("user-deletion-completed");
        consumer.Subscribe("user.deletion.completed");

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
                    var @event = JsonSerializer.Deserialize<UserDeletionCompletedEvent>(result.Message.Value);
                    if (@event is null || string.IsNullOrWhiteSpace(@event.UserId))
                    {
                        logger.LogWarning("無効なイベントデータをスキップ: {Topic}", "user.deletion.completed");
                        consumer.Commit(result);
                        continue;
                    }

                    {
                        using var scope = scopeFactory.CreateScope();
                        var dsrService = scope.ServiceProvider.GetRequiredService<IDsrService>();
                        await dsrService.HandleDeletionCompletedAsync(
                            @event.UserId, @event.ServiceName, @event.Success, @event.Error, stoppingToken);
                        logger.LogInformation("削除完了イベント処理: {UserId}, Service={ServiceName}",
                            @event.UserId, @event.ServiceName);
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
                                { "X-Original-Topic", Encoding.UTF8.GetBytes("user.deletion.completed") }
                            }
                        };
                        await dlProducer.ProduceAsync("user.deletion.completed.dlt", dlMessage, stoppingToken);
                        consumer.Commit(result);
                    }
                    catch (Exception dlEx)
                    {
                        logger.LogError(dlEx, "DLT 転送失敗: {Topic}", "user.deletion.completed.dlt");
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
