using AiSupportService.Configurations;
using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AiSupportService.Infrastructure.HealthChecks;

/// <summary>
/// Kafka クラスターの疎通を確認するヘルスチェック実装。
/// </summary>
/// <remarks>
/// <para>
/// このヘルスチェックは Kubernetes や .NET Aspire の Readiness プローブとして使用され、
/// Kafka クラスターへの接続状態を検証する。イベント駆動アーキテクチャの基盤である
/// Kafka への接続が確立されていることを確認する。
/// </para>
/// <para>
/// 検証方法: Confluent.Kafka の <see cref="IAdminClient"/> を使用してブローカーメタデータを取得し、
/// 1 台以上のブローカーが応答することで接続状態を判定する。タイムアウト（5 秒）を設定する。
/// </para>
/// <para>
/// <b>依存サービス:</b>
/// <list type="bullet">
///   <item><description><see cref="KafkaSettings"/> - Kafka 接続設定（BootstrapServers）</description></item>
/// </list>
/// </para>
/// <para>
/// <b>注意:</b> 毎回 <see cref="AdminClientBuilder"/> で新規クライアントを作成するため、
/// 高頻度のヘルスチェックではオーバーヘッドが発生する可能性がある。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Program.cs でのヘルスチェック登録
/// builder.Services.AddHealthChecks()
///     .AddCheck&lt;KafkaHealthCheck&gt;("kafka", tags: ["ready"]);
/// </code>
/// </example>
/// <param name="kafkaSettings">Kafka 接続設定。BootstrapServers を含む。</param>
/// <param name="logger">診断ログの出力先ロガー。</param>
public class KafkaHealthCheck(
    IOptions<KafkaSettings> kafkaSettings,
    ILogger<KafkaHealthCheck> logger) : IHealthCheck
{
    /// <summary>
    /// Kafka ブローカーへの接続を検証し、ヘルスチェック結果を返す。
    /// </summary>
    /// <param name="context">
    /// ヘルスチェックコンテキスト。登録名やタグ情報を含む。
    /// </param>
    /// <param name="cancellationToken">
    /// キャンセルトークン。外部からのキャンセル要求を受け付ける。
    /// なお、Confluent.Kafka の GetMetadata は同期 API のため、キャンセルトークンは直接使用されない。
    /// </param>
    /// <returns>
    /// <list type="bullet">
    ///   <item><description><see cref="HealthCheckResult.Healthy"/> - 1 台以上のブローカーが応答した場合（ブローカー数を含む）</description></item>
    ///   <item><description><see cref="HealthCheckResult.Unhealthy"/> - ブローカーが見つからない、またはタイムアウト/例外発生時</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// メタデータ取得は同期 API のため、<see cref="Task.FromResult{TResult}"/> でラップして返す。
    /// 5 秒のタイムアウトを設定し、長時間のブロッキングを防止する。
    /// </para>
    /// <para>
    /// <b>ブローカー数の報告:</b> Healthy 時はメッセージにブローカー数を含めるため、
    /// クラスターのスケール状態を監視ダッシュボードで確認可能。
    /// </para>
    /// </remarks>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = new AdminClientConfig
            {
                BootstrapServers = kafkaSettings.Value.BootstrapServers
            };

            using var adminClient = new AdminClientBuilder(config).Build();
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));

            if (metadata.Brokers.Count > 0)
            {
                return Task.FromResult(HealthCheckResult.Healthy(
                    $"Kafka クラスター接続正常: {metadata.Brokers.Count} ブローカー"));
            }

            return Task.FromResult(HealthCheckResult.Unhealthy("Kafka ブローカーが見つかりません"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Kafka ヘルスチェック失敗: {Message}", ex.Message);
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Kafka 接続失敗", ex));
        }
    }
}
