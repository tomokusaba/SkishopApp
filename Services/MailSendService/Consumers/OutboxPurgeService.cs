using MailSendService.Repositories.Interfaces;

namespace MailSendService.Consumers;

/// <summary>
/// 発行済み（PUBLISHED）の Outbox イベントを 7 日後に日次でパージするバックグラウンドサービス。
/// </summary>
/// <remarks>
/// <para>spec.md §データ保持期間に基づき、PUBLISHED ステータスのイベントを 7 日間保持した後に物理削除する。
/// 7 日間の保持によりイベント再送保証の確認期間を確保する。</para>
/// <para>毎日 UTC 04:00 に実行される（PiiCleanupService の 03:00 と重複しないよう 1 時間ずらす）。</para>
/// </remarks>
/// <param name="scopeFactory">Scoped サービス解決用のスコープファクトリ。</param>
/// <param name="timeProvider">テスト可能な時刻プロバイダー。</param>
/// <param name="logger">ロガー。</param>
public class OutboxPurgeService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<OutboxPurgeService> logger) : BackgroundService
{
    /// <summary>
    /// UTC 04:00 のスケジュールに従い、Outbox パージを定期実行するメインループ。
    /// </summary>
    /// <param name="stoppingToken">サービス停止要求を通知するキャンセルトークン。</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxPurgeService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = timeProvider.GetUtcNow();
            var nextRun = now.Date.AddDays(1).AddHours(4);
            if (now.Hour < 4)
                nextRun = now.Date.AddHours(4);

            var delay = nextRun - now;
            if (delay > TimeSpan.Zero)
            {
                logger.LogInformation("OutboxPurgeService next run: {NextRun}", nextRun);
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox purge error: {Message}", ex.Message);
            }
        }

        logger.LogInformation("OutboxPurgeService stopped.");
    }

    /// <summary>
    /// PUBLISHED ステータスで 7 日以上経過した Outbox イベントを物理削除する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    private async Task PurgeAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxEventRepository>();

        var cutoff = timeProvider.GetUtcNow().AddDays(-7);
        var deletedCount = await repository.DeletePublishedOlderThanAsync(cutoff, ct);

        if (deletedCount > 0)
        {
            logger.LogInformation("Outbox purge completed: {DeletedCount} PUBLISHED events older than 7 days removed.", deletedCount);
        }
        else
        {
            logger.LogInformation("Outbox purge: no PUBLISHED events older than 7 days.");
        }
    }
}
