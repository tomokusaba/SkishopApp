using AuthService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.BackgroundServices;

/// <summary>
/// 期限切れのリフレッシュトークンとパスワードリセットトークンをクリーンアップするバックグラウンドサービス。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、データベースの肥大化を防止し、セキュリティを維持するため、
/// 不要になったトークンを定期的に物理削除します。
/// </para>
/// <para>
/// <strong>クリーンアップ対象:</strong>
/// <list type="bullet">
///   <item>
///     <term>リフレッシュトークン</term>
///     <description>有効期限から 7 日以上経過したトークンを削除します。
///     この猶予期間は、トークンローテーション検知のために設けられています。</description>
///   </item>
///   <item>
///     <term>パスワードリセットトークン</term>
///     <description>使用済み（<see cref="AuthService.Models.PasswordReset.IsUsed"/> が true）
///     または有効期限切れのトークンを即座に削除します。</description>
///   </item>
/// </list>
/// </para>
/// <para>
/// <strong>実行間隔:</strong> 1 時間ごとに実行されます。
/// </para>
/// <para>
/// <strong>トークンローテーション:</strong>
/// リフレッシュトークンは使用時に新しいトークンに置き換えられ（トークンローテーション）、
/// 古いトークンは無効化されますが、即座には削除されません。これは、
/// トークン再利用攻撃（Token Replay Attack）を検知するためです。
/// </para>
/// </remarks>
/// <param name="scopeFactory">DI スコープを作成するためのファクトリ。</param>
/// <param name="timeProvider">現在時刻の取得に使用するタイムプロバイダー。</param>
/// <param name="logger">ログ出力に使用するロガー。</param>
public sealed class TokenCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<TokenCleanupService> logger) : BackgroundService
{
    /// <summary>
    /// 処理実行の間隔（1 時間）。
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>
    /// リフレッシュトークンの保持期間（7 日）。
    /// 有効期限からこの期間が経過したトークンが削除対象となります。
    /// </summary>
    /// <remarks>
    /// この期間は、トークン再利用攻撃を検知するために設けられています。
    /// 無効化されたトークンが再利用された場合、同じファミリーの
    /// すべてのトークンを無効化する処理が可能になります。
    /// </remarks>
    private static readonly TimeSpan RefreshTokenRetentionPeriod = TimeSpan.FromDays(7);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await BackgroundServiceHelper.WaitForDatabaseAsync(scopeFactory, logger, stoppingToken);

        logger.LogInformation("TokenCleanupService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredTokensAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "トークンクリーンアップでエラーが発生しました: {Message}", ex.Message);
                await Task.Delay(Interval, stoppingToken);
            }
        }

        logger.LogInformation("TokenCleanupService stopped");
    }

    /// <summary>
    /// 期限切れのトークンをクリーンアップします。
    /// </summary>
    /// <param name="ct">キャンセルを通知するトークン。</param>
    /// <returns>処理完了を表す非同期タスク。</returns>
    /// <remarks>
    /// <para>
    /// EF Core のバルク削除（<see cref="RelationalQueryableExtensions.ExecuteDeleteAsync{TSource}"/>）を
    /// 使用し、効率的にレコードを削除します。
    /// </para>
    /// </remarks>
    private async Task CleanupExpiredTokensAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var now = timeProvider.GetUtcNow();
        var retentionCutoff = now - RefreshTokenRetentionPeriod;

        var deletedRefreshTokens = await context.RefreshTokens
            .Where(r => r.ExpiresAt < retentionCutoff)
            .ExecuteDeleteAsync(ct);

        var deletedPasswordResets = await context.PasswordResets
            .Where(p => (p.IsUsed || p.ExpiresAt < now))
            .ExecuteDeleteAsync(ct);

        if (deletedRefreshTokens > 0 || deletedPasswordResets > 0)
        {
            logger.LogInformation(
                "トークンクリーンアップ完了: RefreshTokens={RefreshTokenCount}, PasswordResets={PasswordResetCount}",
                deletedRefreshTokens, deletedPasswordResets);
        }
    }
}
