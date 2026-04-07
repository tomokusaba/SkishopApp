using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MailSendService.Models;

/// <summary>
/// メール添付ファイルのメタデータを管理する EF Core エンティティ（テーブル: mail_attachments）。
/// </summary>
[Table("mail_attachments")]
public class MailAttachment
{
    /// <summary>添付ファイルレコードの一意識別子（UUID）。</summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>関連するメールログの ID（外部キー）。</summary>
    [Column("mail_log_id")]
    [Required]
    [MaxLength(36)]
    public string MailLogId { get; set; } = string.Empty;

    /// <summary>添付ファイルのファイル名。</summary>
    [Column("filename")]
    [Required]
    [MaxLength(500)]
    public string Filename { get; set; } = string.Empty;

    /// <summary>添付ファイルの MIME コンテンツタイプ。</summary>
    [Column("content_type")]
    [Required]
    [MaxLength(200)]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>インライン添付ファイルのコンテンツ ID（CID）。</summary>
    [Column("content_id")]
    [MaxLength(200)]
    public string? ContentId { get; set; }

    /// <summary>レコード作成日時（UTC）。</summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>レコード最終更新日時（UTC）。</summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>関連するメールログへのナビゲーションプロパティ。</summary>
    public MailLog MailLog { get; set; } = null!;
}
