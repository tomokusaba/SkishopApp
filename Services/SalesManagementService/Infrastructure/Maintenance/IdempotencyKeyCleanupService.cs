using SalesManagementService.Repositories.Interfaces;

namespace SalesManagementService.Infrastructure.Maintenance;

public class IdempotencyKeyCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<IdempotencyKeyCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("IdempotencyKeyCleanupService を開始します");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IIdempotencyKeyRepository>();

                var now = timeProvider.GetUtcNow();
                await repository.DeleteExpiredAsync(now, stoppingToken);

                logger.LogInformation("IdempotencyKey クリーンアップ完了: Threshold={Threshold}", now);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "IdempotencyKey クリーンアップでエラーが発生: {Message}", ex.Message);
            }

            await Task.Delay(CleanupInterval, stoppingToken);
        }

        logger.LogInformation("IdempotencyKeyCleanupService を停止しました");
    }
}
