using UserManagementService.Services.Interfaces;

namespace UserManagementService.BackgroundServices;

/// <summary>
/// DSR タイムアウト監視の BackgroundService。24 時間以上 PROCESSING のままのリクエストを
/// リトライまたは FAILED に遷移させる。1 時間間隔で実行する。
/// </summary>
public class DsrTimeoutMonitorService(
    IServiceScopeFactory scopeFactory,
    ILogger<DsrTimeoutMonitorService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dsrService = scope.ServiceProvider
                    .GetRequiredService<IDsrService>();

                await dsrService.ProcessTimedOutRequestsAsync(stoppingToken);
                logger.LogInformation("DSR タイムアウト監視チェック完了");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "DSR タイムアウト監視でエラーが発生しました: {Message}", ex.Message);
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
