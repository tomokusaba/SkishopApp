using System.ComponentModel.DataAnnotations;

namespace PointService.Configurations;

/// <summary>Kafka 設定。</summary>
public record KafkaSettings
{
    [Required(ErrorMessage = "Kafka BootstrapServers は必須です")]
    public string BootstrapServers { get; init; } = string.Empty;

    [Required(ErrorMessage = "Kafka GroupId は必須です")]
    public string GroupId { get; init; } = "point-service";
}
