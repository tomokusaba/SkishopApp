using System.Text.Json;
using AiSupportService.Infrastructure.Persistence;
using AiSupportService.Models;
using Microsoft.EntityFrameworkCore;

namespace AiSupportService.Infrastructure.Kafka;

/// <summary>
/// データ保持期間ポリシーに基づき、古いデータを定期的にクリーンアップする <see cref="BackgroundService"/>。
/// </summary>
/// <remarks>
/// <para>
/// GDPR およびデータ最小化原則に準拠するため、不要になったデータを自動的に削除・匿名化する。
/// 24 時間間隔で以下のデータを処理する:
/// </para>
/// <para>
/// <b>削除対象:</b>
/// <list type="bullet">
///   <item><description>チャットメッセージ・セッション: 90 日超過</description></item>
///   <item><description>検索分析データ: 180 日超過</description></item>
///   <item><description>レコメンデーション: 有効期限 + 30 日超過</description></item>
///   <item><description>需要予測: 1 年超過</description></item>
///   <item><description>発行済み Outbox イベント: 7 日超過</description></item>
///   <item><description>閲覧履歴: 30 日超過</description></item>
/// </list>
/// </para>
/// <para>
/// <b>匿名化対象:</b>
/// <list type="bullet">
///   <item><description>購買履歴: 1 年超過のデータを月単位で集約・匿名化（統計分析用データは維持）</description></item>
/// </list>
/// </para>
/// <para>
/// <b>排他制御:</b> PostgreSQL のアドバイザリロック（<c>pg_try_advisory_lock</c>）により、
/// 複数インスタンスでの同時実行を防止する。ロック取得に失敗した場合はスキップして次回実行を待つ。
/// </para>
/// <para>
/// <b>バッチ処理:</b> 閲覧履歴・購買履歴は 100 件ずつバッチ処理し、
/// 大量データ処理時のメモリ使用量とトランザクション時間を制御する。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Program.cs での登録
/// builder.Services.AddHostedService&lt;DataRetentionCleanupService&gt;();
/// </code>
/// </example>
/// <param name="scopeFactory">
/// Scoped サービス取得用のファクトリ。<see cref="AppDbContext"/> の取得に使用する。
/// </param>
/// <param name="logger">診断ログの出力先ロガー。</param>
public class DataRetentionCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<DataRetentionCleanupService> logger) : BackgroundService
{
    /// <summary>
    /// クリーンアップの実行間隔（24 時間）。
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    /// <summary>
    /// 定期的にデータ保持ポリシーに基づくクリーンアップを実行する。
    /// </summary>
    /// <param name="stoppingToken">
    /// サービス停止を通知するキャンセルトークン。
    /// このトークンがキャンセルされると、現在の処理完了後にループを終了する。
    /// </param>
    /// <returns>サービス停止まで継続するタスク。</returns>
    /// <remarks>
    /// <para>
    /// 起動後 5 分待機してから最初のクリーンアップを実行する。
    /// これにより、サービス起動直後の負荷集中を回避する。
    /// </para>
    /// <para>
    /// <b>PostgreSQL アドバイザリロック:</b>
    /// <c>pg_try_advisory_lock(hashtext('data_retention_cleanup'))</c> で排他ロックを試行し、
    /// 取得成功時のみクリーンアップを実行する。処理完了後は <c>pg_advisory_unlock</c> で解放する。
    /// </para>
    /// <para>
    /// <b>エラーハンドリング:</b> <see cref="OperationCanceledException"/> 以外の例外は
    /// ログ出力後にリトライ（次回インターバル後）する。クリティカルな障害でもサービスは停止しない。
    /// </para>
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var connection = context.Database.GetDbConnection();
                await connection.OpenAsync(stoppingToken);
                await using var lockCmd = connection.CreateCommand();
                lockCmd.CommandText = "SELECT pg_try_advisory_lock(hashtext('data_retention_cleanup'))";
                var lockResult = await lockCmd.ExecuteScalarAsync(stoppingToken);
                var lockAcquired = lockResult is true;

                if (!lockAcquired)
                {
                    logger.LogInformation("DataRetentionCleanup: 他インスタンスが実行中のためスキップ");
                    await Task.Delay(Interval, stoppingToken);
                    continue;
                }

                try
                {
                    var now = DateTime.UtcNow;

                    var deletedMessages = await context.ChatMessages
                        .Where(m => m.CreatedAt < now.AddDays(-90))
                        .ExecuteDeleteAsync(stoppingToken);

                    var deletedSessions = await context.ChatSessions
                        .Where(s => s.CreatedAt < now.AddDays(-90))
                        .ExecuteDeleteAsync(stoppingToken);

                    var deletedAnalytics = await context.SearchAnalytics
                        .Where(a => a.CreatedAt < now.AddDays(-180))
                        .ExecuteDeleteAsync(stoppingToken);

                    var deletedRecommendations = await context.Recommendations
                        .Where(r => r.ExpiresAt < now.AddDays(-30))
                        .ExecuteDeleteAsync(stoppingToken);

                    var deletedForecasts = await context.DemandForecasts
                        .Where(f => f.CreatedAt < now.AddYears(-1))
                        .ExecuteDeleteAsync(stoppingToken);

                    var deletedOutbox = await context.OutboxEvents
                        .Where(e => e.Status == "PUBLISHED" && e.PublishedAt < now.AddDays(-7))
                        .ExecuteDeleteAsync(stoppingToken);

                    await CleanupBrowsingHistoryAsync(context, now, stoppingToken);
                    await AnonymizePurchaseHistoryAsync(context, now, stoppingToken);

                    logger.LogInformation(
                        "DataRetentionCleanup 完了: Sessions={Sessions}, Messages={Messages}, " +
                        "Analytics={Analytics}, Recommendations={Recommendations}, " +
                        "Forecasts={Forecasts}, Outbox={Outbox}",
                        deletedSessions, deletedMessages, deletedAnalytics,
                        deletedRecommendations, deletedForecasts, deletedOutbox);
                }
                finally
                {
                    await using var unlockCmd = connection.CreateCommand();
                    unlockCmd.CommandText = "SELECT pg_advisory_unlock(hashtext('data_retention_cleanup'))";
                    await unlockCmd.ExecuteScalarAsync(CancellationToken.None);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "DataRetentionCleanup エラー: {Message}", ex.Message);
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    /// <summary>
    /// バッチ処理の 1 回あたりの最大件数。
    /// </summary>
    /// <remarks>
    /// メモリ使用量とトランザクション時間のバランスを考慮して 100 件に設定。
    /// 大量データがある場合は複数バッチに分割して処理される。
    /// </remarks>
    private const int BatchSize = 100;

    /// <summary>
    /// 30 日を超えた閲覧履歴をバッチ単位で削除する。
    /// </summary>
    /// <param name="context">データベースコンテキスト。</param>
    /// <param name="now">現在日時（UTC）。カットオフ日の計算に使用する。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期処理を表すタスク。</returns>
    /// <remarks>
    /// <para>
    /// <see cref="UserProfile.BrowsingHistoryJson"/> に格納された閲覧履歴を JSON としてパースし、
    /// 30 日以上前のエントリを除外した新しい JSON で更新する。
    /// </para>
    /// <para>
    /// <b>バッチ処理:</b> <see cref="BatchSize"/> 件ずつプロファイルを取得・更新し、
    /// 残りがなくなるまで繰り返す。
    /// </para>
    /// </remarks>
    private async Task CleanupBrowsingHistoryAsync(
        AppDbContext context, DateTime now, CancellationToken ct)
    {
        var cutoff = now.AddDays(-30);
        var totalProcessed = 0;
        int batchCount;

        do
        {
            var profiles = await context.UserProfiles
                .Where(p => p.BrowsingHistoryJson != null && p.BrowsingHistoryJson != "[]")
                .OrderBy(p => p.Id)
                .Skip(totalProcessed)
                .Take(BatchSize)
                .ToListAsync(ct);

            batchCount = profiles.Count;

            foreach (var profile in profiles)
            {
                if (string.IsNullOrEmpty(profile.BrowsingHistoryJson)) continue;

                var history = JsonSerializer.Deserialize<List<BrowsingHistoryEntry>>(profile.BrowsingHistoryJson);
                if (history is null) continue;

                var filtered = history.Where(h => h.ViewedAt >= cutoff).ToList();
                if (filtered.Count < history.Count)
                {
                    profile.BrowsingHistoryJson = JsonSerializer.Serialize(filtered);
                }
            }

            await context.SaveChangesAsync(ct);
            totalProcessed += batchCount;
        } while (batchCount == BatchSize);

        logger.LogInformation("閲覧履歴クリーンアップ完了: 対象プロファイル数={Count}", totalProcessed);
    }

    /// <summary>
    /// 1 年を超えた購買履歴を月単位で集約・匿名化する。
    /// </summary>
    /// <param name="context">データベースコンテキスト。</param>
    /// <param name="now">現在日時（UTC）。カットオフ日の計算に使用する。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期処理を表すタスク。</returns>
    /// <remarks>
    /// <para>
    /// 古い購買履歴の個別注文情報を削除し、月単位の集約レコード（<c>ANONYMIZED</c>）に置換することで、
    /// 統計分析に必要な情報を維持しつつ個人情報を保護する。
    /// </para>
    /// <para>
    /// <b>匿名化ルール:</b>
    /// <list type="bullet">
    ///   <item><description><c>OrderId</c>: "ANONYMIZED" に置換</description></item>
    ///   <item><description><c>ProductId</c>: "ANONYMIZED" に置換</description></item>
    ///   <item><description><c>ProductName</c>: "YYYY-MM集約" 形式に置換</description></item>
    ///   <item><description><c>Quantity</c>: 月内の合計数量</description></item>
    ///   <item><description><c>UnitPrice</c>: 月内の平均単価</description></item>
    ///   <item><description><c>PurchasedAt</c>: 月内の最新日時</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>GDPR 対応:</b> 匿名化されたデータは個人データに該当しないため、
    /// GDPR の保持期間制限を超えて統計目的で利用可能。
    /// </para>
    /// </remarks>
    private async Task AnonymizePurchaseHistoryAsync(
        AppDbContext context, DateTime now, CancellationToken ct)
    {
        var cutoff = now.AddYears(-1);
        var totalProcessed = 0;
        int batchCount;

        do
        {
            var profiles = await context.UserProfiles
                .Where(p => p.PurchaseHistoryJson != null && p.PurchaseHistoryJson != "[]")
                .OrderBy(p => p.Id)
                .Skip(totalProcessed)
                .Take(BatchSize)
                .ToListAsync(ct);

            batchCount = profiles.Count;

            foreach (var profile in profiles)
            {
                if (string.IsNullOrEmpty(profile.PurchaseHistoryJson)) continue;

                var history = JsonSerializer.Deserialize<List<PurchaseHistoryEntry>>(profile.PurchaseHistoryJson);
                if (history is null) continue;

                var recent = history.Where(h => h.PurchasedAt >= cutoff).ToList();
                var old = history.Where(h => h.PurchasedAt < cutoff).ToList();

                if (old.Count > 0)
                {
                    var anonymized = old
                        .GroupBy(h => h.PurchasedAt.ToString("yyyy-MM"))
                        .Select(g => new PurchaseHistoryEntry(
                            "ANONYMIZED", "ANONYMIZED", $"{g.Key}集約", g.Sum(h => h.Quantity),
                            g.Average(h => h.UnitPrice), g.Max(h => h.PurchasedAt)))
                        .ToList();

                    recent.AddRange(anonymized);
                    profile.PurchaseHistoryJson = JsonSerializer.Serialize(recent);
                }
            }

            await context.SaveChangesAsync(ct);
            totalProcessed += batchCount;
        } while (batchCount == BatchSize);

        logger.LogInformation("購買履歴匿名化完了: 対象プロファイル数={Count}", totalProcessed);
    }
}
