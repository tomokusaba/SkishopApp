using FluentValidation;
using MailSendService.DTOs.Requests;

namespace MailSendService.Validators;

/// <summary>
/// <see cref="TemplateCreateRequest"/> の FluentValidation バリデーター。
/// </summary>
/// <remarks>
/// テンプレート名の形式、件名の必須・長さ、テンプレートタイプの制約（TRANSACTIONAL / MARKETING）、
/// 本文の長さ制限、および HTML 本文またはテキスト本文の少なくとも一方が必須であることを検証する。
/// </remarks>
public class TemplateCreateRequestValidator : AbstractValidator<TemplateCreateRequest>
{
    /// <summary>許可されるテンプレートタイプの一覧。</summary>
    private static readonly string[] AllowedTypes = ["TRANSACTIONAL", "MARKETING"];

    /// <summary>
    /// バリデーションルールを定義するコンストラクタ。
    /// </summary>
    public TemplateCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100)
            .Matches(@"^[a-z0-9-]+$").WithMessage("テンプレート名は半角英小文字・数字・ハイフンのみ");
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TemplateType).NotEmpty()
            .Must(t => AllowedTypes.Contains(t))
            .WithMessage("テンプレートタイプは TRANSACTIONAL または MARKETING のみ");
        RuleFor(x => x.HtmlBody).MaximumLength(500_000).When(x => x.HtmlBody is not null);
        RuleFor(x => x.TextBody).MaximumLength(500_000).When(x => x.TextBody is not null);
        RuleFor(x => x.Variables).MaximumLength(10_000).When(x => x.Variables is not null);
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.HtmlBody) || !string.IsNullOrWhiteSpace(x.TextBody))
            .WithMessage("HTML 本文またはテキスト本文の少なくとも一方を指定してください");
    }
}
