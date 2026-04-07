using System.ComponentModel.DataAnnotations;

namespace InventoryManagementService.Configurations;

/// <summary>
/// Apache Kafka 接続設定。appsettings.json の "Kafka" セクションにバインドされる。
/// Producer / Consumer 共通の接続パラメータを管理する。
/// </summary>
public record KafkaConfig
{
    /// <summary>Kafka ブローカーのアドレス（ホスト:ポート）。複数指定はカンマ区切り。</summary>
    [Required(ErrorMessage = "Kafka:BootstrapServers は必須です")]
    public string BootstrapServers { get; init; } = string.Empty;

    /// <summary>コンシューマーグループ ID。同一グループ内でパーティションが分散される。</summary>
    public string GroupId { get; init; } = "inventory-service";

    /// <summary>プロデューサーのべき等性を有効にする。重複メッセージの発行を防止する。</summary>
    public bool EnableIdempotence { get; init; } = true;
}
