using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthService.Models;

/// <summary>
/// 監査ログエンティティ。システム内で発生した重要な操作を記録する。
/// コンプライアンス要件およびセキュリティ監査のために使用される。
/// </summary>
/// <remarks>
/// <para>
/// 監査ログは不変であり、一度記録されたログは変更・削除されない。
/// GDPR および SOC 2 コンプライアンスの証跡として使用される。
/// </para>
/// <para>
/// CorrelationId を使用してマイクロサービス間のリクエストを追跡できる。
/// </para>
/// </remarks>
[Table("audit_logs")]
public class AuditLog
{
    /// <summary>
    /// 監査ログの一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// イベントを発生させたマイクロサービスの名前。
    /// </summary>
    [Column("service_name")]
    [Required]
    [MaxLength(100)]
    public string ServiceName { get; set; } = "AuthService";

    /// <summary>
    /// 操作対象のエンティティ型名（例: "User", "Role"）。
    /// </summary>
    [Column("entity_type")]
    [Required]
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// 操作対象のエンティティ ID。
    /// </summary>
    [Column("entity_id")]
    [Required]
    [MaxLength(36)]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// 実行されたアクション（例: "CREATE", "UPDATE", "DELETE"）。
    /// </summary>
    [Column("action")]
    [Required]
    [MaxLength(20)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// アクションを実行したユーザーの ID。システム操作の場合は null。
    /// </summary>
    [Column("actor_id")]
    [MaxLength(36)]
    public string? ActorId { get; set; }

    /// <summary>
    /// アクションを実行したユーザーのロール。
    /// </summary>
    [Column("actor_role")]
    [MaxLength(50)]
    public string? ActorRole { get; set; }

    /// <summary>
    /// リクエスト元の IP アドレス（IPv4/IPv6 対応）。
    /// </summary>
    /// <remarks>
    /// セキュリティ: IP アドレスは監査目的でのみ使用し、マーケティング等には使用しない。
    /// </remarks>
    [Column("ip_address")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// 分散トレーシング用の相関 ID。マイクロサービス間のリクエスト追跡に使用。
    /// </summary>
    [Column("correlation_id")]
    [MaxLength(36)]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 監査ログの作成日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
