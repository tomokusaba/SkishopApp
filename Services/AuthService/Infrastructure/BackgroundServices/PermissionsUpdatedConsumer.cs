using System.Text.Json;
using AuthService.Infrastructure.Persistence;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.BackgroundServices;

/// <summary>
/// ユーザー権限更新イベントを購読し、ローカルのユーザーロールを同期するバックグラウンドサービス。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、他のマイクロサービス（UserManagementService など）から発行される
/// <c>user.permissions.updated</c> トピックのイベントを消費し、AuthService 内の
/// ユーザーロール情報を更新します。
/// </para>
/// <para>
/// <strong>セキュリティ上の重要な処理:</strong>
/// 権限更新時に、対象ユーザーのすべての有効なリフレッシュトークンを無効化します。
/// これにより、権限昇格・降格が即座に反映され、古い権限でのアクセスを防止します。
/// </para>
/// <para>
/// <strong>Kafka コンシューマー設定:</strong>
/// <list type="bullet">
///   <item><term>GroupId</term><description><c>auth-service-permissions-updated</c></description></item>
///   <item><term>AutoOffsetReset</term><description><c>Earliest</c>（未処理のイベントをすべて処理）</description></item>
///   <item><term>AutoCommit</term><description>無効（処理完了後に手動コミット）</description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="scopeFactory">DI スコープを作成するためのファクトリ。</param>
/// <param name="configuration">Kafka ブートストラップサーバーの設定を取得するための構成。</param>
/// <param name="timeProvider">現在時刻の取得に使用するタイムプロバイダー。</param>
/// <param name="logger">ログ出力に使用するロガー。</param>
public sealed class PermissionsUpdatedConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<PermissionsUpdatedConsumer> logger) : BackgroundService
{
    /// <summary>
    /// 権限更新イベントのペイロード。
    /// </summary>
    /// <param name="UserId">更新対象のユーザー ID。</param>
    /// <param name="NewRole">新しいロール名。</param>
    private record PermissionsUpdatedEvent(string UserId, string NewRole);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await BackgroundServiceHelper.WaitForDatabaseAsync(scopeFactory, logger, stoppingToken);

        logger.LogInformation("PermissionsUpdatedConsumer started");

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers が設定されていません"),
            GroupId = "auth-service-permissions-updated",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("user.permissions.updated");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);

                if (result?.Message?.Value is null)
                    continue;

                var updatedEvent = JsonSerializer.Deserialize<PermissionsUpdatedEvent>(result.Message.Value);

                if (updatedEvent is not null)
                {
                    await ProcessPermissionsUpdateAsync(updatedEvent.UserId, updatedEvent.NewRole, stoppingToken);
                }

                consumer.Commit(result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}", ex.ConsumerRecord?.Topic);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "権限更新イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
        logger.LogInformation("PermissionsUpdatedConsumer stopped");
    }

    /// <summary>
    /// 権限更新イベントを処理し、ユーザーのロールを更新します。
    /// </summary>
    /// <param name="userId">更新対象のユーザー ID。</param>
    /// <param name="newRole">新しいロール名。</param>
    /// <param name="ct">キャンセルを通知するトークン。</param>
    /// <returns>処理完了を表す非同期タスク。</returns>
    /// <remarks>
    /// <para>
    /// この処理では以下のステップを実行します:
    /// <list type="number">
    ///   <item><description>対象ユーザーの取得（存在しない場合は警告ログを出力して終了）</description></item>
    ///   <item><description>ユーザーのロールを更新</description></item>
    ///   <item><description>すべての有効なリフレッシュトークンを無効化</description></item>
    ///   <item><description>変更をデータベースに保存</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>セキュリティ考慮事項:</strong>
    /// リフレッシュトークンの無効化により、ユーザーは次回のトークンリフレッシュ時に
    /// 再認証を求められ、新しい権限が反映されたトークンを取得することになります。
    /// </para>
    /// </remarks>
    private async Task ProcessPermissionsUpdateAsync(string userId, string newRole, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            logger.LogWarning("権限更新対象ユーザー未検出: UserId={UserId}", userId);
            return;
        }

        user.Role = Enum.Parse<Enums.UserRoleType>(newRole, ignoreCase: true);

        // Revoke all refresh tokens
        var activeTokens = await context.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = timeProvider.GetUtcNow();
        }

        await context.SaveChangesAsync(ct);

        logger.LogInformation("権限更新完了: UserId={UserId}, NewRole={NewRole}, RevokedTokens={TokenCount}",
            userId, newRole, activeTokens.Count);
    }
}
