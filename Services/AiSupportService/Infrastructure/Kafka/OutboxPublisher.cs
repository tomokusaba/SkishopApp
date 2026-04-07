using System.Text;
using AiSupportService.Infrastructure.Persistence;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;

namespace AiSupportService.Infrastructure.Kafka;

/// <summary>
/// Outbox テーブルの未発行イベントを Kafka に発行する <see cref="BackgroundService"/>。
/// </summary>
/// <remarks>
/// <para>
/// Outbox パターンにより、データベーストランザクションとイベント発行の整合性を保証する。
/// ビジネスロジックは Outbox テーブルにイベントを INSERT し、このサービスが非同期で Kafka に発行する。
/// </para>
/// <para>
/// <b>動的バックオフ:</b>
/// <list type="bullet">
///   <item><description>未発行イベントが存在する場合: 最小間隔（100ms）でポーリング</description></item>
///   <item><description>未発行イベントがない場合: 指数バックオフで間隔を延長（最大 5 秒）</description></item>
/// </list>
/// </para>
/// <para>
/// <b>バッチ処理:</b> 1 回のポーリングで最大 50 件のイベントを処理する。
/// 大量のイベントがある場合でも、個別の発行失敗が他のイベントに影響しないよう設計されている。
/// </para>
/// <para>
/// <b>リトライとフェイルオーバー:</b>
/// <list type="bullet">
///   <item><description>発行失敗時は <c>RetryCount</c> をインクリメント</description></item>
///   <item><description>最大リトライ回数（5 回）を超えたイベントは <c>FAILED</c> ステータスに遷移</description></item>
///   <item><description>失敗イベントはアラート・手動調査の対象となる</description></item>
/// </list>
/// </para>
/// <para>
/// <b>メッセージヘッダー:</b> 各メッセージには <c>event-type</c> と <c>aggregate-type</c> ヘッダーが付与され、
/// コンシューマー側でのルーティングに使用できる。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Program.cs での登録
/// builder.Services.AddSingleton&lt;IProducer&lt;string, string&gt;&gt;(sp =>
///     new ProducerBuilder&lt;string, string&gt;(config).Build());
/// builder.Services.AddHostedService&lt;OutboxPublisher&gt;();
/// </code>
/// </example>
/// <param name="scopeFactory">
/// Scoped サービス取得用のファクトリ。<see cref="AppDbContext"/> の取得に使用する。
/// </param>
/// <param name="producer">Kafka Producer インスタンス。Singleton として DI 登録されていること。</param>
/// <param name="logger">診断ログの出力先ロガー。</param>
public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    /// <summary>
    /// イベント発行の最大リトライ回数。この回数を超えると FAILED ステータスに遷移する。
    /// </summary>
    private const int MaxRetryCount = 5;

    /// <summary>
    /// 1 回のポーリングで処理するイベントの最大件数。
    /// </summary>
    private const int BatchSize = 50;

    /// <summary>
    /// ポーリング間隔の最小値（100 ミリ秒）。
    /// </summary>
    private static readonly TimeSpan MinDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// ポーリング間隔の最大値（5 秒）。
    /// </summary>
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Outbox テーブルから PENDING イベントをバッチ取得し、Kafka に発行する。
    /// </summary>
    /// <param name="stoppingToken">
    /// サービス停止を通知するキャンセルトークン。
    /// このトークンがキャンセルされると、現在の処理完了後にループを終了する。
    /// </param>
    /// <returns>サービス停止まで継続するタスク。</returns>
    /// <remarks>
    /// <para>
    /// <b>処理フロー:</b>
    /// <list type="number">
    ///   <item><description>PENDING かつ RetryCount &lt; MaxRetryCount のイベントをバッチ取得</description></item>
    ///   <item><description>各イベントを Kafka に ProduceAsync</description></item>
    ///   <item><description>成功時: ステータスを PUBLISHED に更新、PublishedAt を設定</description></item>
    ///   <item><description>失敗時: RetryCount をインクリメント、エラーメッセージを記録</description></item>
    ///   <item><description>MaxRetryCount 超過時: ステータスを FAILED に更新</description></item>
    ///   <item><description>変更をデータベースに保存</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>トランザクション:</b> バッチ内の全イベント処理後に一括で SaveChangesAsync を呼び出す。
    /// 個別の ProduceException は他のイベントに影響しない。
    /// </para>
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var currentDelay = MinDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var pendingEvents = await context.OutboxEvents
                    .Where(e => e.Status == "PENDING" && e.RetryCount < MaxRetryCount)
                    .OrderBy(e => e.CreatedAt)
                    .Take(BatchSize)
                    .ToListAsync(stoppingToken);

                if (pendingEvents.Count == 0)
                {
                    currentDelay = TimeSpan.FromMilliseconds(
                        Math.Min(currentDelay.TotalMilliseconds * 2, MaxDelay.TotalMilliseconds));
                    await Task.Delay(currentDelay, stoppingToken);
                    continue;
                }

                currentDelay = MinDelay;

                foreach (var outboxEvent in pendingEvents)
                {
                    try
                    {
                        var message = new Message<string, string>
                        {
                            Key = outboxEvent.AggregateId,
                            Value = outboxEvent.Payload,
                            Headers = new Headers
                            {
                                { "event-type", Encoding.UTF8.GetBytes(outboxEvent.EventType) },
                                { "aggregate-type", Encoding.UTF8.GetBytes(outboxEvent.AggregateType) }
                            }
                        };

                        await producer.ProduceAsync(outboxEvent.Topic, message, stoppingToken);

                        outboxEvent.Status = "PUBLISHED";
                        outboxEvent.PublishedAt = DateTime.UtcNow;

                        logger.LogInformation(
                            "Outbox イベント発行: {EventType}, AggregateId={AggregateId}, Topic={Topic}",
                            outboxEvent.EventType, outboxEvent.AggregateId, outboxEvent.Topic);
                    }
                    catch (ProduceException<string, string> ex)
                    {
                        outboxEvent.RetryCount++;
                        outboxEvent.ErrorMessage = ex.Error.Reason;

                        if (outboxEvent.RetryCount >= MaxRetryCount)
                            outboxEvent.Status = "FAILED";

                        logger.LogError(ex,
                            "Outbox イベント発行失敗: {EventType}, RetryCount={RetryCount}",
                            outboxEvent.EventType, outboxEvent.RetryCount);
                    }
                }

                await context.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "OutboxPublisher エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
