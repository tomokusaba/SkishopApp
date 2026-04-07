using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthService.Infrastructure.Persistence;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.BackgroundServices;

/// <summary>
/// ユーザー削除イベントを購読し、関連するすべての認証データをクリーンアップするバックグラウンドサービス。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、UserManagementService から発行される <c>user.deleted</c> トピックの
/// イベントを消費し、GDPR の「忘れられる権利」に対応するため、ユーザーに関連する
/// すべての認証データを削除または匿名化します。
/// </para>
/// <para>
/// <strong>削除対象データ:</strong>
/// <list type="bullet">
///   <item>OAuth アカウント連携情報</item>
///   <item>リフレッシュトークン</item>
///   <item>ユーザーセッション</item>
///   <item>ユーザーロール割り当て</item>
///   <item>MFA 設定（TOTP 秘密鍵を含む）</item>
///   <item>パスワードリセットトークン</item>
///   <item>パスワード履歴</item>
///   <item>ユーザーアカウント本体</item>
/// </list>
/// </para>
/// <para>
/// <strong>匿名化対象データ:</strong>
/// <list type="bullet">
///   <item>セキュリティログ（ユーザー ID を null に設定、IP アドレスをハッシュ化）</item>
/// </list>
/// </para>
/// <para>
/// <strong>トランザクション保証:</strong>
/// すべての削除・匿名化処理は単一のデータベーストランザクション内で実行され、
/// 部分的な削除を防止します。
/// </para>
/// <para>
/// <strong>Kafka コンシューマー設定:</strong>
/// <list type="bullet">
///   <item><term>GroupId</term><description><c>auth-service-user-deleted</c></description></item>
///   <item><term>AutoOffsetReset</term><description><c>Earliest</c></description></item>
///   <item><term>AutoCommit</term><description>無効（処理完了後に手動コミット）</description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="scopeFactory">DI スコープを作成するためのファクトリ。</param>
/// <param name="configuration">Kafka ブートストラップサーバーの設定を取得するための構成。</param>
/// <param name="logger">ログ出力に使用するロガー。</param>
public sealed class UserDeletedConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<UserDeletedConsumer> logger) : BackgroundService
{
    /// <summary>
    /// ユーザー削除イベントのペイロード。
    /// </summary>
    /// <param name="UserId">削除対象のユーザー ID。</param>
    private record UserDeletedEvent(string UserId);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await BackgroundServiceHelper.WaitForDatabaseAsync(scopeFactory, logger, stoppingToken);

        logger.LogInformation("UserDeletedConsumer started");

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers が設定されていません"),
            GroupId = "auth-service-user-deleted",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("user.deleted");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);

                if (result?.Message?.Value is null)
                    continue;

                var deletedEvent = JsonSerializer.Deserialize<UserDeletedEvent>(result.Message.Value);

                if (deletedEvent?.UserId is not null)
                {
                    await ProcessUserDeletionAsync(deletedEvent.UserId, stoppingToken);
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
                logger.LogError(ex, "ユーザー削除イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
        logger.LogInformation("UserDeletedConsumer stopped");
    }

    /// <summary>
    /// ユーザー削除イベントを処理し、関連するすべてのデータを削除または匿名化します。
    /// </summary>
    /// <param name="userId">削除対象のユーザー ID。</param>
    /// <param name="ct">キャンセルを通知するトークン。</param>
    /// <returns>処理完了を表す非同期タスク。</returns>
    /// <exception cref="Exception">トランザクション内でエラーが発生した場合にスローされます。</exception>
    /// <remarks>
    /// <para>
    /// この処理は単一のデータベーストランザクション内で実行され、
    /// 失敗時には自動的にロールバックされます。
    /// </para>
    /// <para>
    /// <strong>処理順序:</strong>
    /// <list type="number">
    ///   <item><description>OAuth アカウント連携の削除</description></item>
    ///   <item><description>リフレッシュトークンの削除</description></item>
    ///   <item><description>ユーザーセッションの削除</description></item>
    ///   <item><description>ユーザーロール割り当ての削除</description></item>
    ///   <item><description>MFA 設定の削除</description></item>
    ///   <item><description>パスワードリセットトークンの削除</description></item>
    ///   <item><description>パスワード履歴の削除</description></item>
    ///   <item><description>セキュリティログの匿名化</description></item>
    ///   <item><description>ユーザーアカウントの削除</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    private async Task ProcessUserDeletionAsync(string userId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        try
        {
            await context.OAuthAccounts
                .Where(o => o.UserId == userId)
                .ExecuteDeleteAsync(ct);

            await context.RefreshTokens
                .Where(r => r.UserId == userId)
                .ExecuteDeleteAsync(ct);

            await context.UserSessions
                .Where(s => s.UserId == userId)
                .ExecuteDeleteAsync(ct);

            await context.UserRoles
                .Where(ur => ur.UserId == userId)
                .ExecuteDeleteAsync(ct);

            await context.Set<Models.UserMfa>()
                .Where(m => m.UserId == userId)
                .ExecuteDeleteAsync(ct);

            await context.PasswordResets
                .Where(p => p.UserId == userId)
                .ExecuteDeleteAsync(ct);

            await context.PasswordHistories
                .Where(ph => ph.UserId == userId)
                .ExecuteDeleteAsync(ct);

            // Anonymize security logs
            var securityLogs = await context.SecurityLogs
                .Where(s => s.UserId == userId)
                .ToListAsync(ct);

            foreach (var log in securityLogs)
            {
                log.UserId = null;

                if (!string.IsNullOrEmpty(log.IpAddress))
                {
                    log.IpAddress = HashIpAddress(log.IpAddress);
                }
            }

            await context.SaveChangesAsync(ct);

            await context.Users
                .Where(u => u.Id == userId)
                .ExecuteDeleteAsync(ct);

            await transaction.CommitAsync(ct);

            logger.LogInformation("ユーザーデータ削除完了: UserId={UserId}", userId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            logger.LogError(ex, "ユーザーデータ削除失敗: UserId={UserId}", userId);
            throw;
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
    /// セキュリティログの分析は引き続き可能です。
    /// </para>
    /// </remarks>
    private static string HashIpAddress(string ipAddress)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(ipAddress));
        return Convert.ToHexStringLower(hashBytes);
    }
}
