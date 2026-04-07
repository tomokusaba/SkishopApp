using UserManagementService.Services.Interfaces;

namespace UserManagementService.BackgroundServices;

/// <summary>
/// 削除リクエストの猶予期間切れ処理の BackgroundService。1 時間間隔で猶予期間が経過した
/// PENDING リクエストを PROCESSING に遷移し、user.deleted イベントを発行する。
/// </summary>
public class DeletionRequestProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<DeletionRequestProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dsrService = scope.ServiceProvider.GetRequiredService<IDsrService>();
                await dsrService.ProcessExpiredGracePeriodRequestsAsync(stoppingToken);
                logger.LogInformation("削除リクエスト猶予期間チェック完了");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "削除リクエスト処理でエラーが発生しました: {Message}", ex.Message);
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
