using MailSendService.Models;
using MailSendService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog.Context;

namespace MailSendService.Consumers;

/// <summary>
/// P1-14: SENDING ステータスのまま孤児化したメールログをリカバリするバックグラウンドサービス。
/// </summary>
/// <remarks>
/// <para>送信処理中にサービスがクラッシュした場合、SENDING ステータスのまま放置される
/// メールログが発生する可能性がある。このサービスはそのような孤児化したレコードを
/// 定期的に検出し、FAILED ステータスに戻してリトライ対象にする。</para>
/// <para>ポーリング間隔: 10 分。孤児化判定: UpdatedAt から 30 分以上経過。</para>
/// </remarks>
/// <param name="scopeFactory">Scoped サービス解決用のスコープファクトリ。</param>
/// <param name="timeProvider">テスト可能な時刻プロバイダー。</param>
/// <param name="logger">ロガー。</param>
public class SendingOrphanRecoveryService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<SendingOrphanRecoveryService> logger) : BackgroundService
{
    /// <summary>ポーリング間隔（10 分）。</summary>
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(10);

    /// <summary>孤児化と判定する経過時間（30 分）。</summary>
    private static readonly TimeSpan OrphanThreshold = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 定期的に SENDING ステータスの孤児化レコードを検出し、FAILED にリセットする。
    /// </summary>
    /// <param name="stoppingToken">サービス停止要求を通知するキャンセルトークン。</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var _ = LogContext.PushProperty("CorrelationId", $"orphan-recovery-{Guid.NewGuid():N}");
        logger.LogInformation("SendingOrphanRecoveryService started. Interval: {Interval}", PollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollingInterval, stoppingToken);

                using var scope = scopeFactory.CreateScope();
                var mailLogRepository = scope.ServiceProvider.GetRequiredService<IMailLogRepository>();

                var cutoff = timeProvider.GetUtcNow().Add(-OrphanThreshold);
                var orphanedLogs = await mailLogRepository.FindOrphanedSendingAsync(cutoff, 50, stoppingToken);

                if (orphanedLogs.Count == 0)
                {
                    logger.LogDebug("No orphaned SENDING logs found");
                    continue;
                }

                logger.LogWarning("Found {Count} orphaned SENDING logs. Resetting to FAILED.", orphanedLogs.Count);

                foreach (var log in orphanedLogs)
                {
                    // 変更追跡のためにエンティティを再取得
                    var trackedLog = await mailLogRepository.FindByIdAsync(log.Id, trackChanges: true, stoppingToken);
                    if (trackedLog is null || trackedLog.Status != MailLogStatus.Sending)
                        continue;

                    trackedLog.MarkAsFailed("Orphaned SENDING status recovered");
                    logger.LogInformation("Reset orphaned log to FAILED: {MailLogId}", log.Id);
                }

                try
                {
                    await mailLogRepository.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Successfully recovered {Count} orphaned SENDING logs", orphanedLogs.Count);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    logger.LogWarning(ex, "Concurrency conflict during orphan recovery");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SendingOrphanRecoveryService error");
            }
        }

        logger.LogInformation("SendingOrphanRecoveryService stopped.");
    }
}
