using CouponService.Services.Interfaces;

namespace CouponService.BackgroundServices;

public class CampaignStatusService(
    IServiceScopeFactory scopeFactory,
    ILogger<CampaignStatusService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(2);
    private TimeSpan _currentInterval = CheckInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CampaignStatusService 開始");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCampaignStatusAsync(stoppingToken);
                _currentInterval = CheckInterval;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "キャンペーンステータス更新でエラーが発生しました");
                _currentInterval = TimeSpan.FromTicks(
                    Math.Min(_currentInterval.Ticks * 2, MaxBackoff.Ticks));
            }

            await Task.Delay(_currentInterval, stoppingToken);
        }

        logger.LogInformation("CampaignStatusService 終了");
    }

    private async Task ProcessCampaignStatusAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var campaignService = scope.ServiceProvider.GetRequiredService<ICampaignService>();

        await campaignService.CompleteExpiredCampaignsAsync(ct);
    }
}
