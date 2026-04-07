using System.Security.Cryptography;
using System.Text;
using MailSendService.Repositories.Interfaces;

namespace MailSendService.Consumers;

/// <summary>
/// GDPR 準拠のため、保持期限を超過した MailLog レコードの個人情報を匿名化するバックグラウンドサービス。
/// </summary>
/// <remarks>
/// <para>毎日 UTC 03:00 に実行され、作成日から 365 日以上経過したレコードの
/// メールアドレスを SHA-256 ハッシュで置換し、受信者名を null に設定する。</para>
/// <para>匿名化後のメールアドレス形式: <c>anon_{hash先頭16文字}@anonymized.local</c></para>
/// </remarks>
/// <param name="scopeFactory">Scoped サービス解決用のスコープファクトリ。</param>
/// <param name="timeProvider">テスト可能な時刻プロバイダー。</param>
/// <param name="logger">ロガー。</param>
public class PiiCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<PiiCleanupService> logger) : BackgroundService
{
    /// <summary>
    /// UTC 03:00 のスケジュールに従い、PII クリーンアップを定期実行するメインループ。
    /// </summary>
    /// <param name="stoppingToken">サービス停止要求を通知するキャンセルトークン。</param>
    /// <remarks>
    /// 現在時刻が 03:00 未満であれば当日の 03:00 まで、03:00 以降であれば翌日の 03:00 まで待機する。
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PiiCleanupService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = timeProvider.GetUtcNow();
            var nextRun = now.Date.AddDays(1).AddHours(3);
            if (now.Hour < 3)
                nextRun = now.Date.AddHours(3);

            var delay = nextRun - now;
            if (delay > TimeSpan.Zero)
            {
                logger.LogInformation("PiiCleanupService next run: {NextRun}", nextRun);
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PII cleanup error: {Message}", ex.Message);
            }
        }

        logger.LogInformation("PiiCleanupService stopped.");
    }

    /// <summary>
    /// 365 日以上経過した MailLog レコードの個人情報を匿名化する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <remarks>
    /// 1 回の実行で最大 500 件を処理する。メールアドレスは SHA-256 ハッシュに置換し、
    /// 受信者名は null に設定する。既に匿名化済み（@anonymized.local）のレコードは除外される。
    /// </remarks>
    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMailLogRepository>();

        var cutoff = timeProvider.GetUtcNow().AddDays(-365);
        var oldRecords = await repository.FindOlderThanAsync(cutoff, limit: 500, ct);

        if (oldRecords.Count == 0)
        {
            logger.LogInformation("PII cleanup: no records older than 365 days.");
            return;
        }

        logger.LogInformation("PII cleanup: anonymizing {Count} records.", oldRecords.Count);

        foreach (var record in oldRecords)
        {
            record.RecipientEmail = HashEmail(record.RecipientEmail);
            record.RecipientName = null;
        }

        await repository.SaveChangesAsync(ct);
        logger.LogInformation("PII cleanup completed: {Count} records anonymized.", oldRecords.Count);
    }

    /// <summary>
    /// メールアドレスを SHA-256 でハッシュ化し、匿名化されたアドレス文字列を返す。
    /// </summary>
    /// <param name="email">匿名化対象のメールアドレス。</param>
    /// <returns><c>anon_{hash先頭16文字}@anonymized.local</c> 形式の匿名化アドレス。</returns>
    private static string HashEmail(string email)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
        return $"anon_{Convert.ToHexStringLower(hash)[..16]}@anonymized.local";
    }
}
