using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.BackgroundServices;

/// <summary>
/// バックグラウンドサービスで共通的に使用されるヘルパーメソッドを提供する静的クラス。
/// </summary>
/// <remarks>
/// <para>
/// このクラスは、各バックグラウンドサービスの起動時にデータベースの準備完了を待機するための
/// 共通ロジックを提供します。.NET Aspire によるオーケストレーション環境において、
/// PostgreSQL の起動がサービスの起動より遅れる場合に対応します。
/// </para>
/// <para>
/// <strong>使用パターン:</strong>
/// <code>
/// protected override async Task ExecuteAsync(CancellationToken stoppingToken)
/// {
///     await BackgroundServiceHelper.WaitForDatabaseAsync(scopeFactory, logger, stoppingToken);
///     // サービスのメイン処理...
/// }
/// </code>
/// </para>
/// <para>
/// <strong>注意事項:</strong>
/// <list type="bullet">
///   <item><description>指定されたリトライ回数を超えてもデータベースに接続できない場合、エラーログを出力して処理を継続します。</description></item>
///   <item><description>CancellationToken によるキャンセル要求に対応し、即座に処理を中断します。</description></item>
/// </list>
/// </para>
/// </remarks>
public static class BackgroundServiceHelper
{
    /// <summary>
    /// データベースの準備が完了するまで待機します。
    /// </summary>
    /// <param name="scopeFactory">DI スコープを作成するためのファクトリ。</param>
    /// <param name="logger">ログ出力に使用するロガー。</param>
    /// <param name="stoppingToken">キャンセルを通知するトークン。</param>
    /// <param name="maxRetries">最大リトライ回数（デフォルト: 30 回）。</param>
    /// <param name="retryDelay">リトライ間隔（デフォルト: 2 秒）。</param>
    /// <returns>データベースの準備完了を待機する非同期タスク。</returns>
    /// <remarks>
    /// <para>
    /// データベースへの接続可否を確認後、OutboxEvents テーブルへのクエリを実行して
    /// マイグレーションが適用されていることを確認します。
    /// </para>
    /// <para>
    /// <strong>動作フロー:</strong>
    /// <list type="number">
    ///   <item><description><see cref="AuthService.Infrastructure.Persistence.AuthDbContext"/> を DI コンテナから取得</description></item>
    ///   <item><description><see cref="Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.CanConnectAsync"/> で接続を確認</description></item>
    ///   <item><description>OutboxEvents テーブルへのダミークエリを実行してマイグレーション適用を確認</description></item>
    ///   <item><description>失敗時は指定された間隔でリトライ</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task WaitForDatabaseAsync(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        CancellationToken stoppingToken,
        int maxRetries = 30,
        TimeSpan? retryDelay = null)
    {
        var delay = retryDelay ?? TimeSpan.FromSeconds(2);
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider
                    .GetRequiredService<Persistence.AuthDbContext>();
                if (await dbContext.Database.CanConnectAsync(stoppingToken))
                {
                    _ = await dbContext.OutboxEvents.Take(0).ToListAsync(stoppingToken);
                    logger.LogInformation("データベース準備完了");
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("データベース未準備（リトライ {Attempt}/{MaxRetries}）: {Message}",
                    i + 1, maxRetries, ex.Message);
            }
            await Task.Delay(delay, stoppingToken);
        }
        logger.LogError("データベースの準備が完了しませんでした（{MaxRetries} 回リトライ後）", maxRetries);
    }
}
