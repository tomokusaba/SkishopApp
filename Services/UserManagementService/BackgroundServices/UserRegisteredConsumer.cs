using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Serilog.Context;
using UserManagementService.Events;
using UserManagementService.Services;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.BackgroundServices;

/// <summary>
/// Kafka <c>user.registered</c> トピックの Consumer。AuthService からのユーザー登録イベントを受信し、
/// User・UserPreference・MemberRank を初期化する。失敗時は DLT（Dead Letter Topic）に転送する。
/// </summary>
public class UserRegisteredConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<UserRegisteredConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var consumer = consumerFactory.CreateConsumer("user-registered");
        consumer.Subscribe("user.registered");

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
                    var @event = JsonSerializer.Deserialize<UserRegisteredEvent>(result.Message.Value);
                    if (@event is null || string.IsNullOrWhiteSpace(@event.UserId))
                    {
                        logger.LogWarning("無効なイベントデータをスキップ: {Topic}", "user.registered");
                        consumer.Commit(result);
                        continue;
                    }

                    {
                        using var scope = scopeFactory.CreateScope();
                        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
                        await userService.InitializeRegisteredUserAsync(@event, stoppingToken);

                        logger.LogInformation("ユーザー登録イベント処理完了: {UserId}", @event.UserId);
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
                                { "X-Original-Topic", Encoding.UTF8.GetBytes("user.registered") }
                            }
                        };
                        await dlProducer.ProduceAsync("user.registered.dlt", dlMessage, stoppingToken);
                        consumer.Commit(result);
                    }
                    catch (Exception dlEx)
                    {
                        logger.LogError(dlEx, "DLT 転送失敗: {Topic}", "user.registered.dlt");
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
