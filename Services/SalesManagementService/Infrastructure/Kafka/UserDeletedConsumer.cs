using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using SalesManagementService.Infrastructure.Persistence;

namespace SalesManagementService.Infrastructure.Kafka;

public record UserDeletedEvent(string UserId, DateTimeOffset DeletedAt);

public class UserDeletedConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<UserDeletedConsumer> logger) : BackgroundService
{
    private const string DeletedMarker = "[DELETED]";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("UserDeletedConsumer を開始します（GDPR Article 17 対応）");

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = "sales-user-deleted",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("user.deleted");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<UserDeletedEvent>(result.Message.Value);

                if (@event is null)
                {
                    logger.LogWarning("user.deleted イベントのデシリアライズに失敗しました");
                    consumer.Commit(result);
                    continue;
                }

                await AnonymizeUserDataAsync(@event.UserId, stoppingToken);
                consumer.Commit(result);

                logger.LogInformation(
                    "GDPR ユーザーデータ匿名化完了: UserId={HashedUserId}",
                    HashUserId(@event.UserId));
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: Topic={Topic}", ex.ConsumerRecord?.Topic);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "user.deleted イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
        logger.LogInformation("UserDeletedConsumer を停止しました");
    }

    private async Task AnonymizeUserDataAsync(string userId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();

        var hashedCustomerId = HashUserId(userId);

        var orderCount = await context.Orders
            .Where(o => o.CustomerId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.CustomerId, hashedCustomerId)
                .SetProperty(o => o.GuestEmail, (string?)null)
                .SetProperty(o => o.ShippingRecipientName, DeletedMarker)
                .SetProperty(o => o.ShippingPostalCode, DeletedMarker)
                .SetProperty(o => o.ShippingPrefecture, DeletedMarker)
                .SetProperty(o => o.ShippingCity, DeletedMarker)
                .SetProperty(o => o.ShippingAddressLine1, DeletedMarker)
                .SetProperty(o => o.ShippingAddressLine2, DeletedMarker)
                .SetProperty(o => o.ShippingPhoneNumber, DeletedMarker)
                .SetProperty(o => o.Notes, DeletedMarker), ct);

        var orderIds = await context.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == hashedCustomerId)
            .Select(o => o.Id)
            .ToListAsync(ct);

        if (orderIds.Count > 0)
        {
            await context.Set<SalesManagementService.Models.Shipment>()
                .Where(s => orderIds.Contains(s.OrderId))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.ShippingRecipientName, DeletedMarker)
                    .SetProperty(s => s.ShippingPostalCode, DeletedMarker)
                    .SetProperty(s => s.ShippingPrefecture, DeletedMarker)
                    .SetProperty(s => s.ShippingCity, DeletedMarker)
                    .SetProperty(s => s.ShippingAddressLine1, DeletedMarker)
                    .SetProperty(s => s.ShippingAddressLine2, DeletedMarker)
                    .SetProperty(s => s.ShippingPhoneNumber, DeletedMarker), ct);
        }

        logger.LogInformation(
            "ユーザーデータ匿名化: {OrderCount} 件の注文を処理しました",
            orderCount);
    }

    private static string HashUserId(string userId)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(userId));
        return Convert.ToHexStringLower(hashBytes);
    }
}
