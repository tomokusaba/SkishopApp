using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthService.Models;

/// <summary>
/// セキュリティログエンティティ。認証・認可に関連するセキュリティイベントを記録する。
/// インシデント対応、不正アクセス検知、コンプライアンス監査に使用される。
/// </summary>
/// <remarks>
/// <para>
/// 記録されるイベント: ログイン成功/失敗、アカウントロック、パスワード変更、MFA 操作等。
/// 詳細は <see cref="Enums.SecurityEventType"/> を参照。
/// </para>
/// <para>
/// 注意: GDPR 準拠のため、PII（個人情報）はログに含めない。
/// IP アドレス、User-Agent、イベント種別のみを記録する。
/// </para>
/// </remarks>
[Table("security_logs")]
public class SecurityLog
{
    /// <summary>
    /// セキュリティログの一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// イベントに関連するユーザーの ID。匿名リクエスト（ログイン試行等）の場合は null。
    /// </summary>
    [Column("user_id")]
    [MaxLength(36)]
    public string? UserId { get; set; }

    /// <summary>
    /// セキュリティイベントの種別。<see cref="Enums.SecurityEventType"/> の文字列表現。
    /// </summary>
    [Column("event_type")]
    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// リクエスト元の IP アドレス（IPv4/IPv6 対応）。
    /// </summary>
    /// <remarks>
    /// ブルートフォース攻撃の検知、地理的異常アクセスの検出に使用。
    /// </remarks>
    [Column("ip_address")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// リクエスト元のブラウザ/クライアント情報。
    /// </summary>
    /// <remarks>
    /// 注意: User-Agent は偽装可能なため、セキュリティ判断の唯一の根拠としない。
    /// </remarks>
    [Column("user_agent")]
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// イベントの追加詳細情報（JSON 形式または自由テキスト）。
    /// </summary>
    /// <remarks>
    /// 注意: 秘密情報（パスワード、トークン等）をこのフィールドに含めないこと。
    /// </remarks>
    [Column("details")]
    [MaxLength(2000)]
    public string? Details { get; set; }

    /// <summary>
    /// イベントの結果。true: 成功、false: 失敗。
    /// </summary>
    [Column("is_success")]
    public bool IsSuccess { get; set; }

    /// <summary>
    /// イベントの発生日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// このログに関連するユーザーへのナビゲーションプロパティ。
    /// </summary>
    public User? User { get; set; }
}
