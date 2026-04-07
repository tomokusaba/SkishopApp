// ─────────────────────────────────────────────────────────────
// KafkaSettings — Kafka 接続設定
//
// IOptions<KafkaSettings> パターンで型安全に管理する。
// appsettings.json の "Kafka" セクションにバインドする。
// ─────────────────────────────────────────────────────────────

namespace ApiGateway.Configurations;

/// <summary>
/// Kafka 接続設定を保持するレコード。IOptions<T> でのバインディングに対応するため init プロパティを使用。
/// </summary>
public record KafkaSettings
{
    /// <summary>Kafka ブローカーのアドレス（カンマ区切り）。</summary>
    public string BootstrapServers { get; init; } = string.Empty;

    /// <summary>コンシューマーグループ ID（デフォルト: api-gateway）。</summary>
    public string GroupId { get; init; } = "api-gateway";
}
