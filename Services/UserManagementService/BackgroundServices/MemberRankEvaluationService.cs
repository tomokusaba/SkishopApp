using UserManagementService.Services.Interfaces;

namespace UserManagementService.BackgroundServices;

/// <summary>
/// 年次会員ランク評価の BackgroundService。4 月 1 日に分散ロック付きで全ユーザーの
/// ランク評価をバッチ実行する。1 時間間隔でチェックする。
/// </summary>
public class MemberRankEvaluationService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<MemberRankEvaluationService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = timeProvider.GetUtcNow();
                if (now.Month == 4 && now.Day == 1)
                {
                    using var scope = scopeFactory.CreateScope();
                    var memberRankService = scope.ServiceProvider.GetRequiredService<IMemberRankService>();
                    await memberRankService.TryExecuteEvaluationWithLockAsync(stoppingToken);
                    logger.LogInformation("年次会員ランク評価バッチ完了");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "年次ランク評価でエラーが発生しました: {Message}", ex.Message);
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
