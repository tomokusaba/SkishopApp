using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using UserManagementService.Configurations;

namespace UserManagementService.Infrastructure.HealthChecks;

/// <summary>
/// Kafka ブローカーの接続確認ヘルスチェック。AdminClient で5 秒以内にメタデータを取得できるか検証する。
/// </summary>
public class KafkaHealthCheck(IOptions<KafkaSettings> kafkaOptions) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = new AdminClientConfig
            {
                BootstrapServers = kafkaOptions.Value.BootstrapServers
            };

            using var adminClient = new AdminClientBuilder(config).Build();
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));

            return Task.FromResult(metadata.Brokers.Count > 0
                ? HealthCheckResult.Healthy("Kafka connection is available.")
                : HealthCheckResult.Unhealthy("Kafka returned no brokers."));
        }
        catch (Exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Kafka health check failed."));
        }
    }
}
