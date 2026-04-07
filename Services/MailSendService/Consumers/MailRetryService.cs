using MailSendService.Configurations;
using MailSendService.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace MailSendService.Consumers;

/// <summary>
/// 送信失敗したメールを指数バックオフで再送するバックグラウンドサービス。
/// </summary>
/// <remarks>
/// <para>5 秒間隔で失敗メールを検索し、最大リトライ回数未満かつバックオフ時間経過済みの
/// メールに対して再送を試みる。</para>
/// <para>バックオフ計算: <c>InitialIntervalMs × Multiplier^RetryCount</c>（上限 300 秒）。</para>
/// <para>時刻比較には <see cref="TimeProvider"/> を使用し、テスト時に時刻を固定可能にしている。</para>
/// </remarks>
/// <param name="scopeFactory">Scoped サービス解決用のスコープファクトリ。</param>
/// <param name="mailOptions">メール設定（リトライ設定を含む）。</param>
/// <param name="timeProvider">テスト可能な時刻プロバイダー。</param>
/// <param name="logger">ロガー。</param>
public class MailRetryService(
    IServiceScopeFactory scopeFactory,
    IOptions<MailSettings> mailOptions,
    TimeProvider timeProvider,
    ILogger<MailRetryService> logger) : BackgroundService
{
    /// <summary>
    /// リトライ対象の失敗メールを定期的に検索し、指数バックオフに基づいて再送を実行する。
    /// </summary>
    /// <param name="stoppingToken">サービス停止要求を通知するキャンセルトークン。</param>
    /// <remarks>
    /// <para>リトライ条件: ステータスが FAILED、リトライ回数が最大回数未満、
    /// かつ <c>UpdatedAt + バックオフ時間</c> が現在時刻を過ぎていること。</para>
    /// <para>個別のリトライ失敗はログに記録し、他のメールのリトライ処理を継続する。</para>
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MailRetryService started.");
        var retrySettings = mailOptions.Value.Retry;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var mailService = scope.ServiceProvider
                    .GetRequiredService<IMailService>();

                var failedMails = await mailService
                    .GetFailedMailsForRetryAsync(20, stoppingToken);

                if (failedMails.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                logger.LogInformation("Retrying {Count} failed mails.", failedMails.Count);

                foreach (var mail in failedMails)
                {
                    var backoffMs = retrySettings.InitialIntervalMs
                        * Math.Pow(retrySettings.Multiplier, mail.RetryCount);
                    var backoff = TimeSpan.FromMilliseconds(Math.Min(backoffMs, 300_000));

                    if (mail.CreatedAt.Add(backoff) > timeProvider.GetUtcNow())
                    {
                        logger.LogDebug("Retry backoff not elapsed for {MailLogId}, RetryCount={RetryCount}",
                            mail.Id, mail.RetryCount);
                        continue;
                    }

                    try
                    {
                        await mailService.RetryAsync(mail.Id, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Retry failed: {MailLogId}", mail.Id);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MailRetryService error: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        logger.LogInformation("MailRetryService stopped.");
    }
}
