using System.ComponentModel.DataAnnotations;

namespace CouponService.Configurations;

public record CouponSettings
{
    public FraudDetectionSettings FraudDetection { get; init; } = new();
    public CacheSettings Cache { get; init; } = new();
}

public record FraudDetectionSettings
{
    [Range(1, 1440)]
    public int WindowMinutes { get; init; } = 10;
    [Range(1, 1000)]
    public int MaxUsagesInWindow { get; init; } = 3;
}

public record CacheSettings
{
    [Range(1, 1440)]
    public int CouponTtlMinutes { get; init; } = 10;
    [Range(1, 1440)]
    public int UsageTtlMinutes { get; init; } = 5;
}

public record KafkaSettings
{
    [Required]
    public string BootstrapServers { get; init; } = string.Empty;
    public string GroupId { get; init; } = "coupon-service";
    public string CouponEventsTopic { get; init; } = "coupon-events";
    public string OrderEventsTopic { get; init; } = "order-events";
}
