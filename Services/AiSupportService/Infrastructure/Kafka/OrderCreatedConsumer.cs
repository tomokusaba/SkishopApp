using System.Text.Json;
using AiSupportService.Configurations;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace AiSupportService.Infrastructure.Kafka;

/// <summary>
/// Kafka の "order.created" トピックを購読し、注文情報からユーザーの購買履歴を更新する <see cref="BackgroundService"/>。
/// </summary>
/// <remarks>
/// <para>
/// SalesManagementService から発行される注文作成イベントを受信し、
/// ユーザープロファイルの購買履歴に注文明細を追加する。この購買履歴は
/// パーソナライズドレコメンデーションの生成に活用される。
/// </para>
/// <para>
/// <b>処理フロー:</b>
/// <list type="number">
///   <item><description>Kafka からメッセージを受信</description></item>
///   <item><description>JSON デシリアライズで <see cref="OrderCreatedEvent"/> に変換</description></item>
///   <item><description>ユーザープロファイルを取得（存在しなければ新規作成）</description></item>
///   <item><description>注文明細ごとに購買履歴エントリを追加</description></item>
///   <item><description>データベースに保存</description></item>
///   <item><description>Kafka オフセットをコミット</description></item>
/// </list>
/// </para>
/// <para>
/// <b>べき等性:</b> 同一の注文 ID が複数回処理されても、購買履歴に重複エントリが追加される。
/// 厳密なべき等性が必要な場合は、注文 ID での重複チェックを追加すること。
/// </para>
/// <para>
/// <b>エラーハンドリング:</b> 処理失敗時は 5 秒待機後にリトライする。
/// オフセットはコミットされないため、次回起動時に再処理される。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Program.cs での登録
/// builder.Services.AddHostedService&lt;OrderCreatedConsumer&gt;();
/// </code>
/// </example>
/// <param name="scopeFactory">
/// Scoped サービス取得用のファクトリ。<see cref="IUserProfileRepository"/> の取得に使用する。
/// </param>
/// <param name="kafkaSettings">Kafka 接続設定。</param>
/// <param name="logger">診断ログの出力先ロガー。</param>
public class OrderCreatedConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaSettings> kafkaSettings,
    ILogger<OrderCreatedConsumer> logger) : BackgroundService
{
    /// <summary>
    /// Kafka メッセージを継続的にコンシュームし、ユーザープロファイルの購買履歴を更新する。
    /// </summary>
    /// <param name="stoppingToken">
    /// サービス停止を通知するキャンセルトークン。
    /// このトークンがキャンセルされると、Consume のブロッキングが中断される。
    /// </param>
    /// <returns>サービス停止まで継続するタスク。</returns>
    /// <remarks>
    /// <para>
    /// <b>Consumer 設定:</b>
    /// <list type="bullet">
    ///   <item><description><c>AutoOffsetReset.Earliest</c>: 未処理メッセージを最初から読み取る</description></item>
    ///   <item><description><c>EnableAutoCommit = false</c>: 処理成功後に明示的にコミット</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Scoped サービス:</b> <see cref="IServiceScopeFactory.CreateScope"/> で
    /// リクエストごとにスコープを生成し、<see cref="IUserProfileRepository"/> を安全に取得する。
    /// </para>
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaSettings.Value.BootstrapServers,
            GroupId = $"{kafkaSettings.Value.GroupId}-order-created",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("order.created");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<OrderCreatedEvent>(result.Message.Value);

                if (@event is null) continue;

                using var scope = scopeFactory.CreateScope();
                var userProfileRepository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

                var profile = await userProfileRepository.FindByUserIdForUpdateAsync(@event.UserId, stoppingToken);

                if (profile is null)
                {
                    profile = new UserProfile { UserId = @event.UserId };
                    await userProfileRepository.AddAsync(profile, stoppingToken);
                }

                foreach (var item in @event.Items)
                {
                    profile.AddPurchaseHistory(new PurchaseHistoryEntry(
                        @event.OrderId, item.ProductId, item.ProductName, item.Quantity, item.UnitPrice, @event.OccurredAt));
                }

                await userProfileRepository.SaveChangesAsync(stoppingToken);
                consumer.Commit(result);

                logger.LogInformation(
                    "購買履歴更新: UserId={UserId}, OrderId={OrderId}, Items={ItemCount}",
                    @event.UserId, @event.OrderId, @event.Items.Count);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}", ex.ConsumerRecord?.Topic);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "注文イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
