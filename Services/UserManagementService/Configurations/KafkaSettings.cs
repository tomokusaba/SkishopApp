namespace UserManagementService.Configurations;

/// <summary>
/// Kafka 接続設定。<c>appsettings.json</c> の <c>Kafka</c> セクションにバインドされる。
/// </summary>
public record KafkaSettings
{
    [System.ComponentModel.DataAnnotations.Required]
    public string BootstrapServers { get; init; } = string.Empty;
    public string ConsumerGroupId { get; init; } = "user-management-service";
    public string AutoOffsetReset { get; init; } = "earliest";
}
