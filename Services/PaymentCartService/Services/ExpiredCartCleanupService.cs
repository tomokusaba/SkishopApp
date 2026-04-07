using Microsoft.Extensions.Options;
using PaymentCartService.Configurations;
using PaymentCartService.Infrastructure;
using PaymentCartService.Repositories.Interfaces;

namespace PaymentCartService.Services;

public class ExpiredCartCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<CartSettings> cartOptions,
    TimeProvider timeProvider,
    ILogger<ExpiredCartCleanupService> logger) : BackgroundService
{
    private readonly CartSettings _settings = cartOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ExpiredCartCleanupService 開始");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var correlationScope = CorrelationIdScope.Push();
                using var scope = scopeFactory.CreateScope();
                var cartRepository = scope.ServiceProvider.GetRequiredService<ICartRepository>();

                var cutoffDate = timeProvider.GetUtcNow().UtcDateTime.AddDays(-30);
                var deletedCount = await cartRepository.CleanupExpiredCartsAsync(
                    cutoffDate, _settings.CleanupBatchSize, stoppingToken);

                if (deletedCount > 0)
                    logger.LogInformation("期限切れカート削除: DeletedCount={DeletedCount}", deletedCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "ExpiredCartCleanupService エラー: {Message}", ex.Message);
            }

            await Task.Delay(
                TimeSpan.FromMinutes(_settings.CleanupIntervalMinutes), stoppingToken);
        }
    }
}
