using System.ComponentModel.DataAnnotations;

namespace MailSendService.Configurations;

/// <summary>
/// Apache Kafka の接続・購読設定。IOptions<T> でのバインディングに対応するため init プロパティを使用。
/// </summary>
public record KafkaSettings
{
    /// <summary>Kafka ブローカーのアドレス一覧。</summary>
    [Required(ErrorMessage = "Kafka BootstrapServers は必須です")]
    public string BootstrapServers { get; init; } = string.Empty;

    /// <summary>コンシューマーグループ ID。</summary>
    [Required(ErrorMessage = "Kafka GroupId は必須です")]
    public string GroupId { get; init; } = string.Empty;

    /// <summary>購読対象のトピック名。</summary>
    [Required(ErrorMessage = "Kafka Topic は必須です")]
    public string Topic { get; init; } = string.Empty;

    // --- SASL 認証設定（P0-9: Kafka SASL 認証対応） ---

    /// <summary>SASL 認証メカニズム（例: SCRAM-SHA-256, SCRAM-SHA-512）。</summary>
    public string? SaslMechanism { get; init; }

    /// <summary>SASL 認証ユーザー名。</summary>
    public string? SaslUsername { get; init; }

    /// <summary>SASL 認証パスワード（環境変数または user-secrets で管理）。</summary>
    public string? SaslPassword { get; init; }

    /// <summary>セキュリティプロトコル（例: SaslSsl, SaslPlaintext）。</summary>
    public string? SecurityProtocol { get; init; }
}
