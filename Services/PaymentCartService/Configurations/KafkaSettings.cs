using System.ComponentModel.DataAnnotations;

namespace PaymentCartService.Configurations;

/// <summary>
/// Kafka関連の設定
/// </summary>
public record KafkaSettings
{
    /// <summary>
    /// Outbox Publisher の最小ポーリング間隔（ミリ秒）
    /// </summary>
    [Range(50, 10000, ErrorMessage = "OutboxMinPollingIntervalMs は 50〜10000 の範囲で指定してください")]
    public int OutboxMinPollingIntervalMs { get; init; } = 100;

    /// <summary>
    /// Outbox Publisher の最大ポーリング間隔（ミリ秒）
    /// </summary>
    [Range(1000, 60000, ErrorMessage = "OutboxMaxPollingIntervalMs は 1000〜60000 の範囲で指定してください")]
    public int OutboxMaxPollingIntervalMs { get; init; } = 5000;

    /// <summary>
    /// Outbox Publisher のバッチサイズ
    /// </summary>
    [Range(1, 1000, ErrorMessage = "OutboxBatchSize は 1〜1000 の範囲で指定してください")]
    public int OutboxBatchSize { get; init; } = 100;

    /// <summary>
    /// Consumer エラー時のバックオフ間隔（秒）
    /// </summary>
    [Range(1, 60, ErrorMessage = "ConsumerErrorBackoffSeconds は 1〜60 の範囲で指定してください")]
    public int ConsumerErrorBackoffSeconds { get; init; } = 5;
}
