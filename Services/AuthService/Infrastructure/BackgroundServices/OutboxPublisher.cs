using AuthService.Enums;
using AuthService.Infrastructure.Persistence;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.BackgroundServices;

/// <summary>
/// Outbox パターンを実装し、保留中のイベントを Kafka に発行するバックグラウンドサービス。
/// </summary>
/// <remarks>
/// <para>
/// Outbox パターンは、データベース書き込みとイベント発行の整合性を保証するための設計パターンです。
/// このサービスは、<see cref="OutboxEvent"/> テーブルに保存された保留中のイベントを
/// ポーリングし、Kafka トピックに発行します。
/// </para>
/// <para>
/// <strong>動的バックオフ戦略:</strong>
/// <list type="bullet">
///   <item>イベント処理成功時: 最小間隔（100ms）にリセット</item>
///   <item>イベントなし: 間隔を 2 倍に延長（最大 5 秒）</item>
///   <item>初期間隔: 500ms</item>
/// </list>
/// </para>
/// <para>
/// <strong>エラーハンドリング:</strong>
/// <list type="bullet">
///   <item>Kafka 発行失敗時は <see cref="OutboxEvent.RetryCount"/> をインクリメント</item>
///   <item>最大リトライ回数（<see cref="OutboxEvent.MaxRetries"/>）を超えた場合は DEAD_LETTER ステータスに移行</item>
///   <item>エラーメッセージは 2000 文字で切り詰めて保存</item>
/// </list>
/// </para>
/// <para>
/// <strong>関連トピック:</strong>
/// <list type="bullet">
///   <item><c>user.registered</c> - ユーザー登録イベント</item>
///   <item><c>user.updated</c> - ユーザー情報更新イベント</item>
///   <item><c>password.changed</c> - パスワード変更イベント</item>
///   <item><c>user.deleted</c> - ユーザー削除イベント</item>
/// </list>
/// </para>
/// </remarks>
/// <param name="scopeFactory">DI スコープを作成するためのファクトリ。</param>
/// <param name="producer">Kafka プロデューサー。</param>
/// <param name="timeProvider">現在時刻の取得に使用するタイムプロバイダー。</param>
/// <param name="logger">ログ出力に使用するロガー。</param>
public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    TimeProvider timeProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    /// <summary>
    /// 1 回のポーリングで処理するイベントの最大数。
    /// </summary>
    private const int BatchSize = 100;

    /// <summary>
    /// ポーリング間隔の最小値（100 ミリ秒）。
    /// イベント処理成功時はこの間隔にリセットされます。
    /// </summary>
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// ポーリング間隔の最大値（5 秒）。
    /// イベントがない場合でもこの間隔を超えることはありません。
    /// </summary>
    private static readonly TimeSpan MaxInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// ポーリング間隔の初期値（500 ミリ秒）。
    /// </summary>
    private static readonly TimeSpan InitialInterval = TimeSpan.FromMilliseconds(500);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await BackgroundServiceHelper.WaitForDatabaseAsync(scopeFactory, logger, stoppingToken);

        logger.LogInformation("OutboxPublisher started");
        var currentInterval = InitialInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedCount = await PublishPendingEventsAsync(stoppingToken);

                currentInterval = processedCount > 0
                    ? MinInterval
                    : TimeSpan.FromMilliseconds(Math.Min(currentInterval.TotalMilliseconds * 2, MaxInterval.TotalMilliseconds));

                await Task.Delay(currentInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutboxPublisher でエラーが発生しました: {Message}", ex.Message);
                await Task.Delay(MaxInterval, stoppingToken);
            }
        }

        logger.LogInformation("OutboxPublisher stopped");
    }

    /// <summary>
    /// 保留中のイベントを Kafka に発行します。
    /// </summary>
    /// <param name="ct">キャンセルを通知するトークン。</param>
    /// <returns>処理したイベント数。</returns>
    /// <remarks>
    /// <para>
    /// PENDING ステータスのイベントを作成日時順にバッチ取得し、
    /// 各イベントを対応する Kafka トピックに発行します。
    /// </para>
    /// <para>
    /// <strong>ステータス遷移:</strong>
    /// <list type="bullet">
    ///   <item>PENDING → PUBLISHED（発行成功）</item>
    ///   <item>PENDING → PENDING（リトライ可能な失敗）</item>
    ///   <item>PENDING → DEAD_LETTER（最大リトライ回数超過）</item>
    /// </list>
    /// </para>
    /// </remarks>
    private async Task<int> PublishPendingEventsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var now = timeProvider.GetUtcNow();

        var pendingEvents = await context.OutboxEvents
            .Where(e => e.Status == OutboxStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pendingEvents.Count == 0)
            return 0;

        var processedCount = 0;

        foreach (var evt in pendingEvents)
        {
            try
            {
                var message = new Message<string, string>
                {
                    Key = evt.Key ?? evt.Id,
                    Value = evt.Payload
                };

                await producer.ProduceAsync(evt.Topic, message, ct);

                evt.Status = OutboxStatus.Published;
                evt.ProcessedAt = now;
                processedCount++;
            }
            catch (ProduceException<string, string> ex)
            {
                evt.RetryCount++;
                evt.ErrorMessage = TruncateMessage(ex.Message);

                if (evt.RetryCount >= evt.MaxRetries)
                {
                    evt.Status = OutboxStatus.DeadLetter;
                    logger.LogError(ex, "Outbox イベント最終失敗（DEAD_LETTER）: EventId={EventId}, Topic={Topic}", evt.Id, evt.Topic);
                }
                else
                {
                    logger.LogWarning(ex, "Outbox イベントリトライ: EventId={EventId}, RetryCount={RetryCount}", evt.Id, evt.RetryCount);
                }
            }
        }

        await context.SaveChangesAsync(ct);

        if (processedCount > 0)
            logger.LogInformation("Outbox イベント発行完了: {Count} 件", processedCount);

        return processedCount;
    }

    /// <summary>
    /// エラーメッセージを最大 2000 文字に切り詰めます。
    /// </summary>
    /// <param name="message">切り詰めるメッセージ。</param>
    /// <returns>切り詰められたメッセージ。</returns>
    private static string TruncateMessage(string message)
        => message.Length <= 2000 ? message : message[..2000];
}
