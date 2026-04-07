using PointService.Repositories.Interfaces;

namespace PointService.BackgroundServices;

public class TierRecalculationNotifier(
    IServiceScopeFactory scopeFactory,
    ILogger<TierRecalculationNotifier> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var accountRepository = scope.ServiceProvider.GetRequiredService<IPointAccountRepository>();
                var tierRepository = scope.ServiceProvider.GetRequiredService<ITierDefinitionRepository>();

                var tiers = await tierRepository.FindAllAsync(stoppingToken);
                logger.LogInformation(
                    "ティア再計算通知: {TierCount} ティア定義を使用して再計算を実行",
                    tiers.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "TierRecalculationNotifier 処理エラー: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromDays(30), stoppingToken);
        }
    }
}
