using System.Text.Json;
using AiSupportService.Configurations;
using AiSupportService.Infrastructure.Persistence;
using AiSupportService.Models;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiSupportService.Infrastructure.Kafka;

/// <summary>
/// Kafka の "user.deletion.requested" トピックを購読し、GDPR 等のデータ削除要求に対応する <see cref="BackgroundService"/>。
/// </summary>
/// <remarks>
/// <para>
/// GDPR（EU 一般データ保護規則）の「忘れられる権利」（Right to Erasure）に対応するため、
/// ユーザーに関連する全データを完全に削除する。削除完了後は Outbox 経由で完了イベントを発行し、
/// オーケストレーターに通知する。
/// </para>
/// <para>
/// <b>削除対象データ:</b>
/// <list type="bullet">
///   <item><description><see cref="ChatMessage"/>: ユーザーのチャットメッセージ</description></item>
///   <item><description><see cref="ChatSession"/>: ユーザーのチャットセッション</description></item>
///   <item><description><see cref="Recommendation"/>: ユーザー向けレコメンデーション</description></item>
///   <item><description><see cref="SearchAnalytics"/>: ユーザーの検索分析データ</description></item>
///   <item><description><see cref="UserProfile"/>: ユーザープロファイル（閲覧履歴・購買履歴含む）</description></item>
/// </list>
/// </para>
/// <para>
/// <b>トランザクション保証:</b> 全削除処理と Outbox イベント追加は単一トランザクション内で実行される。
/// 部分的な削除は発生しない。
/// </para>
/// <para>
/// <b>セキュリティ:</b> 削除要求イベントには RequestId が含まれ、監査ログとの紐付けが可能。
/// 削除完了後のデータは復元不可能。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Program.cs での登録
/// builder.Services.AddHostedService&lt;UserDeletionConsumer&gt;();
/// </code>
/// </example>
/// <param name="scopeFactory">
/// Scoped サービス取得用のファクトリ。<see cref="AppDbContext"/> の取得に使用する。
/// </param>
/// <param name="kafkaSettings">Kafka 接続設定。</param>
/// <param name="logger">診断ログの出力先ロガー。</param>
public class UserDeletionConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaSettings> kafkaSettings,
    ILogger<UserDeletionConsumer> logger) : BackgroundService
{
    /// <summary>
    /// Kafka メッセージを継続的にコンシュームし、ユーザーデータの完全削除を実行する。
    /// </summary>
    /// <param name="stoppingToken">
    /// サービス停止を通知するキャンセルトークン。
    /// このトークンがキャンセルされると、Consume のブロッキングが中断される。
    /// </param>
    /// <returns>サービス停止まで継続するタスク。</returns>
    /// <remarks>
    /// <para>
    /// <b>処理フロー:</b>
    /// <list type="number">
    ///   <item><description>Kafka からメッセージを受信</description></item>
    ///   <item><description>JSON デシリアライズで <see cref="UserDeletionRequestedEvent"/> に変換</description></item>
    ///   <item><description>トランザクション開始</description></item>
    ///   <item><description>関連データの一括削除（ExecuteDeleteAsync）</description></item>
    ///   <item><description><see cref="UserDeletionCompletedEvent"/> を Outbox に追加</description></item>
    ///   <item><description>トランザクションコミット</description></item>
    ///   <item><description>Kafka オフセットをコミット</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>エラーハンドリング:</b> トランザクション内で例外が発生した場合はロールバックし、
    /// 5 秒待機後にリトライする。オフセットはコミットされないため、次回起動時に再処理される。
    /// </para>
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaSettings.Value.BootstrapServers,
            GroupId = $"{kafkaSettings.Value.GroupId}-user-deletion",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("user.deletion.requested");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<UserDeletionRequestedEvent>(result.Message.Value);

                if (@event is null) continue;

                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await using var transaction = await context.Database.BeginTransactionAsync(stoppingToken);
                try
                {
                    var userId = @event.UserId;

                    await context.ChatMessages
                        .Where(m => context.ChatSessions.Any(s => s.UserId == userId && s.Id == m.SessionId))
                        .ExecuteDeleteAsync(stoppingToken);
                    await context.ChatSessions.Where(s => s.UserId == userId).ExecuteDeleteAsync(stoppingToken);
                    await context.Recommendations.Where(r => r.UserId == userId).ExecuteDeleteAsync(stoppingToken);
                    await context.SearchAnalytics.Where(a => a.UserId == userId).ExecuteDeleteAsync(stoppingToken);
                    await context.UserProfiles.Where(p => p.UserId == userId).ExecuteDeleteAsync(stoppingToken);

                    var completionEvent = new OutboxEvent
                    {
                        AggregateType = "UserProfile",
                        AggregateId = userId,
                        EventType = "UserDeletionCompleted",
                        Topic = "user.deletion.completed.ai-support",
                        Payload = JsonSerializer.Serialize(new UserDeletionCompletedEvent(
                            userId, @event.RequestId, "AiSupportService", DateTime.UtcNow))
                    };
                    await context.OutboxEvents.AddAsync(completionEvent, stoppingToken);
                    await context.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);

                    logger.LogInformation("ユーザーデータ削除完了: UserId={UserId}", userId);
                }
                catch
                {
                    await transaction.RollbackAsync(stoppingToken);
                    throw;
                }

                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}", ex.ConsumerRecord?.Topic);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "ユーザー削除処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
