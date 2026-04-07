using System.ComponentModel.DataAnnotations;

namespace PointService.Configurations;

/// <summary>ポイントサービス設定（IOptions パターン）。</summary>
public record PointSettings
{
    [Range(1, 100)]
    public int BaseRate { get; init; } = 1;

    [Range(1, 10000)]
    public int BaseUnit { get; init; } = 100;

    [Range(1, 120)]
    public int ExpirationMonths { get; init; } = 12;

    [Range(1, 10000)]
    public int ExpiryBatchSize { get; init; } = 1000;

    [Range(1, 168)]
    public int ExpiryCheckIntervalHours { get; init; } = 24;
}
