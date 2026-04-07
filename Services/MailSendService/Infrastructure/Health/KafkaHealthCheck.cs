using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MailSendService.Infrastructure.Health;

/// <summary>
/// Kafka ブローカーへの接続を検証するヘルスチェック。
/// </summary>
/// <remarks>
/// AdminClient を使用してブローカーメタデータを取得し、
/// 利用可能なブローカーが 1 つ以上存在すれば Healthy と判定する。
/// タイムアウトは 5 秒に設定されている。
/// </remarks>
/// <param name="bootstrapServers">Kafka ブローカーの接続文字列。</param>
public class KafkaHealthCheck(string bootstrapServers) : IHealthCheck
{
    /// <summary>
    /// Kafka ブローカーへの接続を確認し、ヘルスチェック結果を返す。
    /// </summary>
    /// <param name="context">ヘルスチェックコンテキスト。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>ブローカー接続可能な場合は <see cref="HealthCheckResult.Healthy"/>、
    /// 不可能な場合は <see cref="HealthCheckResult.Unhealthy"/>。</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var config = new AdminClientConfig { BootstrapServers = bootstrapServers };
            using var adminClient = new AdminClientBuilder(config).Build();
            var metadata = await Task.Run(() => adminClient.GetMetadata(TimeSpan.FromSeconds(5)), ct);
            return metadata.Brokers.Count > 0
                ? HealthCheckResult.Healthy($"Kafka reachable: {metadata.Brokers.Count} broker(s)")
                : HealthCheckResult.Unhealthy("No Kafka brokers available");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Health check was cancelled");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Kafka unreachable", ex);
        }
    }
}
