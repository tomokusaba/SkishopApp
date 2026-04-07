using Confluent.Kafka;
using Microsoft.Extensions.Options;
using UserManagementService.Configurations;

namespace UserManagementService.Services;

/// <summary>
/// Kafka Consumer ファクトリインターフェース。
/// </summary>
public interface IKafkaConsumerFactory
{
    /// <summary>
    /// <see cref="KafkaSettings"/> に基づき Kafka Consumer を生成する。
    /// </summary>
    /// <param name="groupIdSuffix">Consumer Group ID のサフィックス。省略時はデフォルト GroupId 。</param>
    IConsumer<string, string> CreateConsumer(string? groupIdSuffix = null);
}

/// <summary>
/// <see cref="KafkaSettings"/> に基づき Kafka Consumer を生成するファクトリ。
/// AutoCommit 無効・ AutoOffsetReset.Earliest で構成する。
/// </summary>
public class KafkaConsumerFactory(IOptions<KafkaSettings> kafkaOptions) : IKafkaConsumerFactory
{
    private readonly KafkaSettings _settings = kafkaOptions.Value;

    public IConsumer<string, string> CreateConsumer(string? groupIdSuffix = null)
    {
        var groupId = groupIdSuffix is not null
            ? $"{_settings.ConsumerGroupId}-{groupIdSuffix}"
            : _settings.ConsumerGroupId;

        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
        return new ConsumerBuilder<string, string>(config).Build();
    }
}
