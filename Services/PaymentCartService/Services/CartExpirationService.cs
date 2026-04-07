using Microsoft.Extensions.Options;
using PaymentCartService.Configurations;
using PaymentCartService.Infrastructure;
using PaymentCartService.Repositories.Interfaces;

namespace PaymentCartService.Services;

public class CartExpirationService(
    IServiceScopeFactory scopeFactory,
    IOptions<CartSettings> cartOptions,
    TimeProvider timeProvider,
    ILogger<CartExpirationService> logger) : BackgroundService
{
    private readonly CartSettings _settings = cartOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CartExpirationService 開始");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var correlationScope = CorrelationIdScope.Push();
                using var scope = scopeFactory.CreateScope();
                var cartRepository = scope.ServiceProvider.GetRequiredService<ICartRepository>();

                var now = timeProvider.GetUtcNow().UtcDateTime;
                var expiredCount = await cartRepository.ExpireCartsAsync(
                    now, _settings.ExpirationBatchSize, stoppingToken);

                if (expiredCount > 0)
                    logger.LogInformation("カート期限切れ更新: ExpiredCount={ExpiredCount}", expiredCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "CartExpirationService エラー: {Message}", ex.Message);
            }

            await Task.Delay(
                TimeSpan.FromMinutes(_settings.ExpirationCheckIntervalMinutes), stoppingToken);
        }
    }
}
