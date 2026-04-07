using System.Text.Json.Serialization;

namespace MailSendService.Consumers;

/// <summary>
/// Kafka イベントのエンベロープ（外包）レコード。イベントメタデータとペイロードを保持する。
/// </summary>
/// <remarks>
/// Kafka "mail-events" トピックから受信した JSON メッセージのデシリアライズに使用する。
/// </remarks>
public record EventEnvelope
{
    /// <summary>イベントの一意識別子。重複排除に使用する。</summary>
    public string EventId { get; init; } = string.Empty;

    /// <summary>イベントタイプ（例: "user.registered", "order.created"）。ルーティングキーとして使用する。</summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>イベントを発行したサービス名。</summary>
    public string Producer { get; init; } = string.Empty;

    /// <summary>分散トレーシング用の相関 ID。未設定の場合は null。</summary>
    public string? CorrelationId { get; init; }

    /// <summary>イベントのペイロード JSON 文字列。イベントタイプに応じた詳細データを格納する。</summary>
    [JsonPropertyName("payload")]
    public string PayloadJson { get; init; } = "{}";
}
