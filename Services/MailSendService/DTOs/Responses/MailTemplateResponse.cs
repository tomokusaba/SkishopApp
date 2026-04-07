namespace MailSendService.DTOs.Responses;

/// <summary>
/// メールテンプレートの API レスポンス DTO。
/// </summary>
/// <param name="Id">テンプレートの一意識別子。</param>
/// <param name="Name">テンプレートの一意名称。</param>
/// <param name="Subject">メール件名テンプレート。</param>
/// <param name="HtmlBody">HTML 形式の本文テンプレート。</param>
/// <param name="TextBody">テキスト形式の本文テンプレート。</param>
/// <param name="TemplateType">テンプレート種別。</param>
/// <param name="Variables">使用可能な変数定義（JSON）。</param>
/// <param name="IsActive">テンプレートの有効フラグ。</param>
/// <param name="CreatedAt">作成日時。</param>
/// <param name="UpdatedAt">最終更新日時。</param>
public record MailTemplateResponse(
    string Id,
    string Name,
    string Subject,
    string? HtmlBody,
    string? TextBody,
    string TemplateType,
    string? Variables,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
