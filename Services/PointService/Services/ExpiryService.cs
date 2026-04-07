using System.Text.Json;
using PointService.Events;
using PointService.Models;
using PointService.Repositories.Interfaces;
using PointService.Services.Interfaces;

namespace PointService.Services;

public class ExpiryService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ExpiryService> logger) : IExpiryService
{
    public async Task<int> ProcessExpiredPointsAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var expiryRepository = scope.ServiceProvider.GetRequiredService<IPointExpiryRepository>();
        var accountRepository = scope.ServiceProvider.GetRequiredService<IPointAccountRepository>();
        var transactionRepository = scope.ServiceProvider.GetRequiredService<IPointTransactionRepository>();
        var outboxEventRepository = scope.ServiceProvider.GetRequiredService<IOutboxEventRepository>();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiredBatch = await expiryRepository.FindExpiredAsync(now, 500, ct);

        if (expiredBatch.Count == 0)
            return 0;

        await using var transaction = await accountRepository.BeginTransactionAsync(ct);
        try
        {
            var processedCount = 0;
            var accountCache = new Dictionary<string, PointAccount>();

            foreach (var expiry in expiredBatch)
            {
                expiry.Status = ExpiryStatuses.Expired;

                if (!accountCache.TryGetValue(expiry.AccountId, out var account))
                {
                    account = await accountRepository.FindByIdAsync(expiry.AccountId, ct);
                    if (account is not null)
                        accountCache[expiry.AccountId] = account;
                }

                if (account is not null)
                {
                    account.ExpirePoints(expiry.Points);

                    await transactionRepository.AddAsync(new PointTransaction
                    {
                        AccountId = account.Id,
                        UserId = expiry.UserId,
                        Type = TransactionTypes.Expire,
                        Points = -expiry.Points,
                        BalanceAfter = account.AvailablePoints,
                        ReferenceId = expiry.Id,
                        ReferenceType = ReferenceTypes.Expiry,
                        Description = "ポイント有効期限切れ"
                    }, ct);

                    await outboxEventRepository.AddAsync(new OutboxEvent
                    {
                        AggregateType = AggregateTypes.PointAccount,
                        AggregateId = account.Id,
                        EventType = EventTopics.PointExpired,
                        Payload = JsonSerializer.Serialize(new PointsExpiredEvent(
                            expiry.UserId.ToString(), expiry.Points, account.AvailablePoints, "", now))
                    }, ct);

                    processedCount++;
                }
            }

            await accountRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            logger.LogInformation("ポイント失効バッチ処理完了: ProcessedCount={Count}", processedCount);
            return processedCount;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            logger.LogError(ex, "ポイント失効バッチ処理エラー");
            throw;
        }
    }

    public async Task<List<PointExpiry>> GetExpiringPointsAsync(
        string userId, int withinDays = 30, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var expiryRepository = scope.ServiceProvider.GetRequiredService<IPointExpiryRepository>();

        return await expiryRepository.FindExpiringWithinAsync(userId, withinDays, ct);
    }
}
