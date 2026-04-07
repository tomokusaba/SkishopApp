using Microsoft.EntityFrameworkCore;
using SalesManagementService.Infrastructure.Persistence;

namespace SalesManagementService.Infrastructure.Outbox;

public class OutboxCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<OutboxCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxCleanupService を開始します");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();

                var threshold = timeProvider.GetUtcNow().Add(-RetentionPeriod);

                var deletedCount = await context.OutboxEvents
                    .Where(e => e.Status == "PUBLISHED" && e.PublishedAt < threshold)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deletedCount > 0)
                {
                    logger.LogInformation(
                        "Outbox クリーンアップ完了: {DeletedCount} 件の PUBLISHED イベントを削除しました",
                        deletedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox クリーンアップでエラーが発生: {Message}", ex.Message);
            }

            await Task.Delay(CleanupInterval, stoppingToken);
        }

        logger.LogInformation("OutboxCleanupService を停止しました");
    }
}
