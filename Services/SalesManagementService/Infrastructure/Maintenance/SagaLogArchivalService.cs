using Microsoft.EntityFrameworkCore;
using SalesManagementService.Infrastructure.Persistence;

namespace SalesManagementService.Infrastructure.Maintenance;

public class SagaLogArchivalService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<SagaLogArchivalService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SagaLogArchivalService を開始します");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();

                var threshold = timeProvider.GetUtcNow().Add(-RetentionPeriod);

                var deletedCount = await context.SagaLogs
                    .Where(s => (s.Status == "COMPLETED" || s.Status == "COMPENSATED")
                        && s.UpdatedAt < threshold)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deletedCount > 0)
                {
                    logger.LogInformation(
                        "SagaLog アーカイブ完了: {DeletedCount} 件の完了済み SagaLog を削除しました",
                        deletedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SagaLog アーカイブでエラーが発生: {Message}", ex.Message);
            }

            await Task.Delay(CleanupInterval, stoppingToken);
        }

        logger.LogInformation("SagaLogArchivalService を停止しました");
    }
}
