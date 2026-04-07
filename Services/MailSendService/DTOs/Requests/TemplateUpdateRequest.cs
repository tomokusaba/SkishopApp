using System.ComponentModel.DataAnnotations;

namespace MailSendService.DTOs.Requests;

/// <summary>
/// メールテンプレート更新のリクエスト DTO。null のフィールドは更新対象外となる。
/// </summary>
/// <param name="Subject">更新後のメール件名（任意）。</param>
/// <param name="HtmlBody">更新後の HTML 本文（任意）。P1-20: 最大 256KB に制限。</param>
/// <param name="TextBody">更新後のテキスト本文（任意）。</param>
/// <param name="Variables">更新後の変数定義 JSON（任意）。</param>
/// <param name="IsActive">テンプレートの有効/無効フラグ（任意）。</param>
public record TemplateUpdateRequest(
    [StringLength(500)]
    string? Subject,
    [StringLength(262144)]  // P1-20: 256KB 制限
    string? HtmlBody,
    [StringLength(262144)]  // P1-20: 256KB 制限
    string? TextBody,
    [StringLength(10000)]
    string? Variables,
    bool? IsActive);
