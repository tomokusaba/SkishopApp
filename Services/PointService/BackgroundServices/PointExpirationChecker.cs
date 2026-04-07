using System.Text.Json;
using Microsoft.Extensions.Options;
using PointService.Configurations;
using PointService.Events;
using PointService.Models;
using PointService.Repositories.Interfaces;

namespace PointService.BackgroundServices;

public class PointExpirationChecker(
    IServiceScopeFactory scopeFactory,
    IOptions<PointSettings> options,
    TimeProvider timeProvider,
    ILogger<PointExpirationChecker> logger) : BackgroundService
{
    private readonly PointSettings _settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var expiryRepository = scope.ServiceProvider.GetRequiredService<IPointExpiryRepository>();
                var accountRepository = scope.ServiceProvider.GetRequiredService<IPointAccountRepository>();
                var transactionRepository = scope.ServiceProvider.GetRequiredService<IPointTransactionRepository>();
                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxEventRepository>();

                var now = timeProvider.GetUtcNow().UtcDateTime;
                var batchSize = _settings.ExpiryBatchSize;

                var expiredBatch = await expiryRepository.FindExpiredAsync(now, batchSize, stoppingToken);

                if (expiredBatch.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                    continue;
                }

                await using var transaction = await accountRepository.BeginTransactionAsync(stoppingToken);
                try
                {
                    var accountCache = new Dictionary<string, PointAccount>();

                    foreach (var expiry in expiredBatch)
                    {
                        expiry.Status = ExpiryStatuses.Expired;

                        if (!accountCache.TryGetValue(expiry.AccountId, out var account))
                        {
                            account = await accountRepository.FindByIdAsync(expiry.AccountId, stoppingToken);
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
                            }, stoppingToken);

                            await outboxRepository.AddAsync(new OutboxEvent
                            {
                                AggregateType = AggregateTypes.PointAccount,
                                AggregateId = account.Id,
                                EventType = EventTopics.PointExpired,
                                Payload = JsonSerializer.Serialize(new PointsExpiredEvent(
                                    expiry.UserId.ToString(), expiry.Points,
                                    account.AvailablePoints, "", now))
                            }, stoppingToken);
                        }
                    }

                    await accountRepository.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);

                    logger.LogInformation(
                        "ポイント失効バッチ処理完了: BatchSize={BatchSize}", expiredBatch.Count);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(stoppingToken);
                    logger.LogError(ex, "ポイント失効バッチ処理エラー");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "PointExpirationChecker 処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
