using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MailSendService.Models;

/// <summary>
/// メールテンプレートを管理する EF Core エンティティ（テーブル: mail_templates）。
/// </summary>
/// <remarks>
/// DDD Aggregate Root として設計。有効化・無効化は専用メソッドで行う。
/// </remarks>
[Table("mail_templates")]
public class MailTemplate
{
    /// <summary>テンプレートの一意識別子（UUID）。</summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>テンプレートの一意名称。</summary>
    [Column("name")]
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>メールの件名テンプレート。</summary>
    [Column("subject")]
    [Required]
    [MaxLength(500)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>HTML 形式のメール本文テンプレート。</summary>
    [Column("html_body")]
    public string? HtmlBody { get; private set; }

    /// <summary>テキスト形式のメール本文テンプレート。</summary>
    [Column("text_body")]
    public string? TextBody { get; private set; }

    /// <summary>テンプレート種別（<see cref="MailTemplateType"/> の定数値）。</summary>
    [Column("template_type")]
    [Required]
    [MaxLength(30)]
    public string TemplateType { get; set; } = MailTemplateType.Transactional;

    /// <summary>テンプレートで使用可能な変数の定義（JSON 形式）。</summary>
    [Column("variables", TypeName = "jsonb")]
    public string? Variables { get; set; }

    /// <summary>テンプレートが有効かどうかを示すフラグ。</summary>
    [Column("is_active")]
    public bool IsActive { get; private set; } = true;

    /// <summary>レコード作成日時（UTC）。</summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>レコード最終更新日時（UTC）。</summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // --- DDD ドメインメソッド（P0-8b: Anemic Domain Model 対策） ---

    /// <summary>
    /// テンプレートを有効化する。
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// テンプレートを無効化する。
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// テンプレートの本文を更新する。
    /// </summary>
    /// <param name="htmlBody">HTML 形式の本文（null 可）。</param>
    /// <param name="textBody">テキスト形式の本文（null 可）。</param>
    /// <exception cref="ArgumentException">HtmlBody と TextBody が両方とも null の場合。</exception>
    public void UpdateBody(string? htmlBody, string? textBody)
    {
        if (string.IsNullOrWhiteSpace(htmlBody) && string.IsNullOrWhiteSpace(textBody))
            throw new ArgumentException("HtmlBody または TextBody のいずれかは必須です");
        HtmlBody = htmlBody;
        TextBody = textBody;
    }

    /// <summary>
    /// テンプレートの件名を更新する。
    /// </summary>
    /// <param name="subject">新しい件名。</param>
    /// <exception cref="ArgumentException">件名が空の場合。</exception>
    public void UpdateSubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject は必須です", nameof(subject));
        Subject = subject;
    }
}
