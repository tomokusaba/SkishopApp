using AuthService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.BackgroundServices;

/// <summary>
/// 期限切れのユーザーセッションを無効化するバックグラウンドサービス。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、セッション管理のセキュリティを強化するため、
/// 有効期限（<see cref="AuthService.Models.UserSession.ExpiresAt"/>）を超えた
/// セッションを定期的に無効化します。
/// </para>
/// <para>
/// <strong>セッション管理の設計:</strong>
/// <list type="bullet">
///   <item>
///     <term>明示的なセッション管理</term>
///     <description>JWT トークンの有効期限とは別に、サーバーサイドでセッションの
///     有効期限を管理することで、トークンの即時無効化が可能になります。</description>
///   </item>
///   <item>
///     <term>デバイス単位のセッション</term>
///     <description>各セッションにはデバイス情報が関連付けられており、
///     特定のデバイスからのセッションのみを無効化できます。</description>
///   </item>
/// </list>
/// </para>
/// <para>
/// <strong>実行間隔:</strong> 5 分ごとに実行されます。
/// </para>
/// <para>
/// <strong>パフォーマンス最適化:</strong>
/// EF Core のバルク更新（<see cref="RelationalQueryableExtensions.ExecuteUpdateAsync{TSource}"/>）を
/// 使用し、大量のセッションを効率的に無効化します。
/// </para>
/// </remarks>
/// <param name="scopeFactory">DI スコープを作成するためのファクトリ。</param>
/// <param name="timeProvider">現在時刻の取得に使用するタイムプロバイダー。</param>
/// <param name="logger">ログ出力に使用するロガー。</param>
public sealed class SessionTimeoutService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<SessionTimeoutService> logger) : BackgroundService
{
    /// <summary>
    /// 処理実行の間隔（5 分）。
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await BackgroundServiceHelper.WaitForDatabaseAsync(scopeFactory, logger, stoppingToken);

        logger.LogInformation("SessionTimeoutService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireTimedOutSessionsAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "セッションタイムアウト処理でエラーが発生しました: {Message}", ex.Message);
                await Task.Delay(Interval, stoppingToken);
            }
        }

        logger.LogInformation("SessionTimeoutService stopped");
    }

    /// <summary>
    /// 期限切れのセッションを無効化します。
    /// </summary>
    /// <param name="ct">キャンセルを通知するトークン。</param>
    /// <returns>処理完了を表す非同期タスク。</returns>
    /// <remarks>
    /// <para>
    /// 無効化対象の条件:
    /// <list type="bullet">
    ///   <item><see cref="AuthService.Models.UserSession.IsActive"/> が true</item>
    ///   <item><see cref="AuthService.Models.UserSession.ExpiresAt"/> が現在時刻より前</item>
    /// </list>
    /// </para>
    /// </remarks>
    private async Task ExpireTimedOutSessionsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var now = timeProvider.GetUtcNow();

        var expiredCount = await context.UserSessions
            .Where(s => s.IsActive && s.ExpiresAt < now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(session => session.IsActive, false), ct);

        if (expiredCount > 0)
        {
            logger.LogInformation("期限切れセッション無効化完了: {Count} 件", expiredCount);
        }
    }
}
