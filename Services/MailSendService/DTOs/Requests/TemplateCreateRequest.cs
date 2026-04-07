using System.ComponentModel.DataAnnotations;

namespace MailSendService.DTOs.Requests;

/// <summary>
/// メールテンプレート新規作成のリクエスト DTO。
/// </summary>
/// <param name="Name">テンプレートの一意名称。</param>
/// <param name="Subject">メール件名のテンプレート。</param>
/// <param name="HtmlBody">HTML 形式の本文テンプレート（任意）。P1-20: 最大 256KB に制限。</param>
/// <param name="TextBody">テキスト形式の本文テンプレート（任意）。</param>
/// <param name="TemplateType">テンプレート種別（TRANSACTIONAL / MARKETING）。</param>
/// <param name="Variables">使用可能な変数定義の JSON 文字列（任意）。</param>
public record TemplateCreateRequest(
    [Required, StringLength(100)]
    string Name,
    [Required, StringLength(500)]
    string Subject,
    [StringLength(262144)]  // P1-20: 256KB 制限
    string? HtmlBody,
    [StringLength(262144)]  // P1-20: 256KB 制限
    string? TextBody,
    [Required, StringLength(30)]
    string TemplateType,
    [StringLength(10000)]
    string? Variables);
