using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MailSendService.Models;

/// <summary>
/// 配信停止メールアドレスを管理する EF Core エンティティ（テーブル: mail_suppressions）。
/// </summary>
[Table("mail_suppressions")]
public class MailSuppression
{
    /// <summary>サプレッションレコードの一意識別子（UUID）。</summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>配信停止対象のメールアドレス。</summary>
    [Column("email")]
    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    /// <summary>配信停止の理由（<see cref="SuppressionReason"/> の定数値）。</summary>
    [Column("reason")]
    [Required]
    [MaxLength(30)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>配信停止が適用された日時。</summary>
    [Column("suppressed_at")]
    public DateTimeOffset SuppressedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>レコード作成日時（UTC）。</summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>レコード最終更新日時（UTC）。</summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
