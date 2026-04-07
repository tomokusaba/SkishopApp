using AuthService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.BackgroundServices;

/// <summary>
/// ログイン失敗回数のリセットとアカウントの自動ロック解除を行うバックグラウンドサービス。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、ブルートフォース攻撃対策として実装されたアカウントロックアウト機能と
/// 連携して動作します。以下の 2 つの処理を定期的に実行します。
/// </para>
/// <para>
/// <strong>セキュリティ機能:</strong>
/// <list type="bullet">
///   <item>
///     <term>自動ロック解除</term>
///     <description>30 分以上ロック状態が継続しているアカウントを自動的に解除します。
///     これにより、正規ユーザーが永久にロックアウトされることを防ぎます。</description>
///   </item>
///   <item>
///     <term>失敗回数リセット</term>
///     <description>15 分以上更新がないアカウントの失敗回数を 0 にリセットします。
///     これにより、長時間経過後のログイン試行が正常に評価されます。</description>
///   </item>
/// </list>
/// </para>
/// <para>
/// <strong>実行間隔:</strong> 5 分ごとに実行されます。
/// </para>
/// <para>
/// <strong>関連コンポーネント:</strong>
/// <list type="bullet">
///   <item><see cref="AuthService.Services.AuthService"/> - ログイン試行とロックアウト判定</item>
///   <item><see cref="AuthMetrics"/> - AccountLockouts メトリクス</item>
/// </list>
/// </para>
/// </remarks>
/// <param name="scopeFactory">DI スコープを作成するためのファクトリ。</param>
/// <param name="timeProvider">現在時刻の取得に使用するタイムプロバイダー。</param>
/// <param name="logger">ログ出力に使用するロガー。</param>
public sealed class FailedAttemptResetService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<FailedAttemptResetService> logger) : BackgroundService
{
    /// <summary>
    /// 処理実行の間隔（5 分）。
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 自動ロック解除までの待機時間（30 分）。
    /// この時間を超えてロック状態が継続しているアカウントは自動的に解除されます。
    /// </summary>
    private static readonly TimeSpan AutoUnlockDuration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 失敗回数リセットまでの待機時間（15 分）。
    /// この時間以上更新がないアカウントの失敗回数は 0 にリセットされます。
    /// </summary>
    private static readonly TimeSpan AttemptResetDuration = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await BackgroundServiceHelper.WaitForDatabaseAsync(scopeFactory, logger, stoppingToken);

        logger.LogInformation("FailedAttemptResetService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ResetFailedAttemptsAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ログイン試行リセットでエラーが発生しました: {Message}", ex.Message);
                await Task.Delay(Interval, stoppingToken);
            }
        }

        logger.LogInformation("FailedAttemptResetService stopped");
    }

    /// <summary>
    /// ロックアウトの自動解除と失敗回数のリセット処理を実行します。
    /// </summary>
    /// <param name="ct">キャンセルを通知するトークン。</param>
    /// <returns>処理完了を表す非同期タスク。</returns>
    /// <remarks>
    /// <para>
    /// EF Core の <see cref="RelationalQueryableExtensions.ExecuteUpdateAsync{TSource}"/> を使用して
    /// バルク更新を実行し、パフォーマンスを最適化しています。
    /// </para>
    /// </remarks>
    private async Task ResetFailedAttemptsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var now = timeProvider.GetUtcNow();
        var unlockCutoff = now - AutoUnlockDuration;
        var resetCutoff = now - AttemptResetDuration;

        // Auto-unlock accounts locked for more than 30 minutes
        var unlockedCount = await context.Users
            .Where(u => u.IsAccountLocked && u.LockedAt != null && u.LockedAt < unlockCutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.IsAccountLocked, false)
                .SetProperty(u => u.FailedLoginAttempts, 0)
                .SetProperty(u => u.LockedAt, (DateTimeOffset?)null), ct);

        // Reset failed attempts for non-locked accounts idle for more than 15 minutes
        var resetCount = await context.Users
            .Where(u => !u.IsAccountLocked && u.FailedLoginAttempts > 0 && u.UpdatedAt < resetCutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.FailedLoginAttempts, 0), ct);

        if (unlockedCount > 0 || resetCount > 0)
        {
            logger.LogInformation(
                "ログイン試行リセット完了: AutoUnlocked={UnlockedCount}, AttemptReset={ResetCount}",
                unlockedCount, resetCount);
        }
    }
}
