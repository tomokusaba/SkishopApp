using System.Text.Json;
using AiSupportService.Configurations;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace AiSupportService.Infrastructure.Kafka;

/// <summary>
/// Kafka の商品関連トピックを購読し、AI 検索用の商品インデックスを同期する <see cref="BackgroundService"/>。
/// </summary>
/// <remarks>
/// <para>
/// InventoryManagementService から発行される商品イベントを受信し、
/// Azure AI Search のインデックスを最新状態に保つ。現在の実装ではログ出力のみ行い、
/// 実際のインデックス更新は <see cref="IProductClient"/> を介して行う設計。
/// </para>
/// <para>
/// <b>購読トピック:</b>
/// <list type="bullet">
///   <item><description><c>product.created</c>: 新規商品の作成</description></item>
///   <item><description><c>product.updated</c>: 既存商品の更新</description></item>
///   <item><description><c>product.deleted</c>: 商品の削除</description></item>
/// </list>
/// </para>
/// <para>
/// <b>処理フロー:</b>
/// <list type="number">
///   <item><description>Kafka からメッセージを受信</description></item>
///   <item><description>トピック種別に応じてイベントをデシリアライズ</description></item>
///   <item><description>インデックス更新/削除を実行（現在はログ出力のみ）</description></item>
///   <item><description>Kafka オフセットをコミット</description></item>
/// </list>
/// </para>
/// <para>
/// <b>エラーハンドリング:</b> 処理失敗時は 5 秒待機後にリトライする。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Program.cs での登録
/// builder.Services.AddHostedService&lt;ProductIndexSyncConsumer&gt;();
/// </code>
/// </example>
/// <param name="kafkaSettings">Kafka 接続設定。</param>
/// <param name="logger">診断ログの出力先ロガー。</param>
public class ProductIndexSyncConsumer(
    IOptions<KafkaSettings> kafkaSettings,
    ILogger<ProductIndexSyncConsumer> logger) : BackgroundService
{
    /// <summary>
    /// Kafka メッセージを継続的にコンシュームし、商品インデックスの追加・更新・削除を処理する。
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
    /// <b>トピック判定:</b> <c>result.Topic</c> でトピック種別を判定し、
    /// 適切なイベント型にデシリアライズする。
    /// </para>
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaSettings.Value.BootstrapServers,
            GroupId = $"{kafkaSettings.Value.GroupId}-product-index",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(["product.created", "product.updated", "product.deleted"]);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var topic = result.Topic;

                if (topic is "product.created" or "product.updated")
                {
                    var @event = JsonSerializer.Deserialize<ProductUpdatedEvent>(result.Message.Value);
                    if (@event is not null)
                    {
                        logger.LogInformation(
                            "商品インデックス更新: ProductId={ProductId}, EventType={EventType}",
                            @event.ProductId, @event.EventType);
                    }
                }
                else if (topic == "product.deleted")
                {
                    var @event = JsonSerializer.Deserialize<ProductDeletedEvent>(result.Message.Value);
                    if (@event is not null)
                    {
                        logger.LogInformation(
                            "商品インデックス削除: ProductId={ProductId}", @event.ProductId);
                    }
                }

                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}", ex.ConsumerRecord?.Topic);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "商品インデックス同期エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
