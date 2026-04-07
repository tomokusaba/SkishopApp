using System.Security.Cryptography;
using System.Text;
using AuthService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.BackgroundServices;

/// <summary>
/// セキュリティログの個人情報匿名化と古いログの物理削除を行うバックグラウンドサービス。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、GDPR や個人情報保護法に準拠するため、セキュリティログに含まれる
/// IP アドレスなどの個人情報を匿名化し、保持期限を超えたログを物理削除します。
/// </para>
/// <para>
/// <strong>データ保持ポリシー:</strong>
/// <list type="bullet">
///   <item>
///     <term>匿名化閾値（90 日）</term>
///     <description>90 日以上前のログの IP アドレスを SHA-256 ハッシュに置換します。
///     これにより、統計的な分析は可能なまま、個人の特定を防止します。</description>
///   </item>
///   <item>
///     <term>削除閾値（365 日）</term>
///     <description>1 年以上前のログを物理削除します。監査要件との兼ね合いで
///     この期間が設定されています。</description>
///   </item>
/// </list>
/// </para>
/// <para>
/// <strong>実行間隔:</strong> 6 時間ごとに実行されます。
/// </para>
/// <para>
/// <strong>バッチ処理:</strong>
/// 大量のレコードを処理する際のメモリ消費とデータベース負荷を抑制するため、
/// 1000 件ずつバッチ処理を行います。
/// </para>
/// <para>
/// <strong>匿名化アルゴリズム:</strong>
/// IP アドレスは SHA-256 でハッシュ化されます。ハッシュ値は 64 文字の 16 進数文字列として
/// 保存され、同一 IP アドレスからのアクセスパターンの分析は可能です。
/// </para>
/// </remarks>
/// <param name="scopeFactory">DI スコープを作成するためのファクトリ。</param>
/// <param name="timeProvider">現在時刻の取得に使用するタイムプロバイダー。</param>
/// <param name="logger">ログ出力に使用するロガー。</param>
public sealed class SecurityLogAnonymizationService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<SecurityLogAnonymizationService> logger) : BackgroundService
{
    /// <summary>
    /// 処理実行の間隔（6 時間）。
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    /// <summary>
    /// 1 回のバッチで処理するレコード数。
    /// </summary>
    private const int BatchSize = 1000;

    /// <summary>
    /// IP アドレス匿名化の閾値（90 日）。
    /// この期間を超えたログの IP アドレスはハッシュ化されます。
    /// </summary>
    private static readonly TimeSpan AnonymizationThreshold = TimeSpan.FromDays(90);

    /// <summary>
    /// ログ物理削除の閾値（365 日）。
    /// この期間を超えたログは完全に削除されます。
    /// </summary>
    private static readonly TimeSpan DeletionThreshold = TimeSpan.FromDays(365);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await BackgroundServiceHelper.WaitForDatabaseAsync(scopeFactory, logger, stoppingToken);

        logger.LogInformation("SecurityLogAnonymizationService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAnonymizationAsync(stoppingToken);
                await ProcessDeletionAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "セキュリティログ匿名化でエラーが発生しました: {Message}", ex.Message);
                await Task.Delay(Interval, stoppingToken);
            }
        }

        logger.LogInformation("SecurityLogAnonymizationService stopped");
    }

    /// <summary>
    /// セキュリティログの IP アドレスを匿名化します。
    /// </summary>
    /// <param name="ct">キャンセルを通知するトークン。</param>
    /// <returns>処理完了を表す非同期タスク。</returns>
    /// <remarks>
    /// <para>
    /// 匿名化対象の判定条件:
    /// <list type="bullet">
    ///   <item>作成日時が匿名化閾値より前</item>
    ///   <item>IP アドレスが null でない</item>
    ///   <item>IP アドレスが 64 文字でない（既にハッシュ化されていない）</item>
    /// </list>
    /// </para>
    /// </remarks>
    private async Task ProcessAnonymizationAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var now = timeProvider.GetUtcNow();
        var anonymizationCutoff = now - AnonymizationThreshold;
        var totalAnonymized = 0;

        while (!ct.IsCancellationRequested)
        {
            var logsToAnonymize = await context.SecurityLogs
                .Where(s => s.CreatedAt < anonymizationCutoff && s.IpAddress != null && s.IpAddress.Length != 64)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (logsToAnonymize.Count == 0)
                break;

            foreach (var log in logsToAnonymize)
            {
                if (!string.IsNullOrEmpty(log.IpAddress))
                {
                    log.IpAddress = HashIpAddress(log.IpAddress);
                }
            }

            await context.SaveChangesAsync(ct);
            totalAnonymized += logsToAnonymize.Count;

            if (logsToAnonymize.Count < BatchSize)
                break;
        }

        if (totalAnonymized > 0)
        {
            logger.LogInformation("セキュリティログ匿名化完了: {Count} 件", totalAnonymized);
        }
    }

    /// <summary>
    /// 保持期限を超えたセキュリティログを物理削除します。
    /// </summary>
    /// <param name="ct">キャンセルを通知するトークン。</param>
    /// <returns>処理完了を表す非同期タスク。</returns>
    /// <remarks>
    /// <para>
    /// EF Core の <see cref="RelationalQueryableExtensions.ExecuteDeleteAsync{TSource}"/> を使用して
    /// バルク削除を実行し、メモリ消費を最小化しています。
    /// </para>
    /// </remarks>
    private async Task ProcessDeletionAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var now = timeProvider.GetUtcNow();
        var deletionCutoff = now - DeletionThreshold;
        var totalDeleted = 0;

        while (!ct.IsCancellationRequested)
        {
            var deletedCount = await context.SecurityLogs
                .Where(s => s.CreatedAt < deletionCutoff)
                .Take(BatchSize)
                .ExecuteDeleteAsync(ct);

            totalDeleted += deletedCount;

            if (deletedCount < BatchSize)
                break;
        }

        if (totalDeleted > 0)
        {
            logger.LogInformation("セキュリティログ物理削除完了: {Count} 件", totalDeleted);
        }
    }

    /// <summary>
    /// IP アドレスを SHA-256 でハッシュ化します。
    /// </summary>
    /// <param name="ipAddress">ハッシュ化する IP アドレス。</param>
    /// <returns>64 文字の小文字 16 進数文字列。</returns>
    /// <remarks>
    /// <para>
    /// ハッシュ化により、IP アドレスから個人を特定することは困難になりますが、
    /// 同一 IP アドレスからのアクセスパターンは分析可能です。
    /// </para>
    /// </remarks>
    private static string HashIpAddress(string ipAddress)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(ipAddress));
        return Convert.ToHexStringLower(hashBytes);
    }
}
