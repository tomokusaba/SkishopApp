using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Serilog.Context;
using UserManagementService.Events;
using UserManagementService.Services;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.BackgroundServices;

/// <summary>
/// Kafka <c>password.changed</c> トピックの Consumer。パスワード変更時にアクティビティ記録と
/// LastLoginAt リセットを行う。
/// </summary>
public class PasswordChangedConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<PasswordChangedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var consumer = consumerFactory.CreateConsumer("password-changed");
        consumer.Subscribe("password.changed");

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
                    var @event = JsonSerializer.Deserialize<PasswordChangedEvent>(result.Message.Value);
                    if (@event is null || string.IsNullOrWhiteSpace(@event.UserId))
                    {
                        logger.LogWarning("無効なイベントデータをスキップ: {Topic}", "password.changed");
                        consumer.Commit(result);
                        continue;
                    }

                    {
                        using var scope = scopeFactory.CreateScope();
                        var activityService = scope.ServiceProvider.GetRequiredService<IActivityService>();
                        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

                        await activityService.RecordAsync(
                            @event.UserId, "PASSWORD_CHANGED",
                            "パスワードが変更されました", null, null, stoppingToken);

                        await userService.ResetLastLoginAtAsync(@event.UserId, stoppingToken);

                        logger.LogInformation("パスワード変更イベント処理完了: {UserId}", @event.UserId);
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
                                { "X-Original-Topic", Encoding.UTF8.GetBytes("password.changed") }
                            }
                        };
                        await dlProducer.ProduceAsync("password.changed.dlt", dlMessage, stoppingToken);
                        consumer.Commit(result);
                    }
                    catch (Exception dlEx)
                    {
                        logger.LogError(dlEx, "DLT 転送失敗: {Topic}", "password.changed.dlt");
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
