using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using PaymentCartService.Infrastructure.Kafka.Events;
using PaymentCartService.Infrastructure.Persistence;

namespace PaymentCartService.Infrastructure.Kafka;

public class UserDeletedConsumer(
    Func<string, IConsumer<string, string>> consumerFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<UserDeletedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var consumer = consumerFactory("user-deleted");
        consumer.Subscribe("user.deleted");
        logger.LogInformation("UserDeletedConsumer 開始: Topic=user.deleted");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                using var correlationScope = CorrelationIdScope.Push();
                var @event = JsonSerializer.Deserialize<UserDeletedEvent>(result.Message.Value);

                if (@event is not null)
                {
                    using var scope = scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var hashedUserId = HashUserId(@event.UserId);

                    await context.Carts
                        .Where(c => c.CustomerId == @event.UserId)
                        .ExecuteUpdateAsync(s => s.SetProperty(c => c.CustomerId, (string?)null),
                            stoppingToken);

                    await context.Payments
                        .Where(p => p.CustomerId == @event.UserId)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.CustomerId, hashedUserId),
                            stoppingToken);

                    logger.LogInformation(
                        "ユーザーデータ匿名化完了: HashedUserId={HashedUserId}", hashedUserId);
                }

                consumer.Commit(result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "UserDeleted イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private static string HashUserId(string userId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userId));
        return $"DELETED_{Convert.ToHexStringLower(hash)[..16]}";
    }
}
