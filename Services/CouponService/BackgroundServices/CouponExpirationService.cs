using CouponService.Services.Interfaces;

namespace CouponService.BackgroundServices;

public class CouponExpirationService(
    IServiceScopeFactory scopeFactory,
    ILogger<CouponExpirationService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(4);
    private TimeSpan _currentInterval = CheckInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CouponExpirationService 開始");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredCouponsAsync(stoppingToken);
                _currentInterval = CheckInterval;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "クーポン期限切れ処理でエラーが発生しました");
                _currentInterval = TimeSpan.FromTicks(
                    Math.Min(_currentInterval.Ticks * 2, MaxBackoff.Ticks));
            }

            await Task.Delay(_currentInterval, stoppingToken);
        }

        logger.LogInformation("CouponExpirationService 終了");
    }

    private async Task ProcessExpiredCouponsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var couponService = scope.ServiceProvider.GetRequiredService<ICouponService>();

        await couponService.DeactivateExpiredCouponsAsync(ct);
    }
}
