using FluentValidation;
using MailSendService.DTOs.Requests;

namespace MailSendService.Validators;

/// <summary>
/// <see cref="TemplateUpdateRequest"/> の FluentValidation バリデーター。
/// </summary>
/// <remarks>
/// 件名・本文・変数の長さ制限を検証し、少なくとも 1 つのフィールドが指定されていることを保証する。
/// 全フィールドが null の場合はバリデーションエラーとなる。
/// </remarks>
public class TemplateUpdateRequestValidator : AbstractValidator<TemplateUpdateRequest>
{
    /// <summary>
    /// バリデーションルールを定義するコンストラクタ。
    /// </summary>
    public TemplateUpdateRequestValidator()
    {
        RuleFor(x => x.Subject).MaximumLength(500).When(x => x.Subject is not null);
        RuleFor(x => x.HtmlBody).MaximumLength(500_000).When(x => x.HtmlBody is not null);
        RuleFor(x => x.TextBody).MaximumLength(500_000).When(x => x.TextBody is not null);
        RuleFor(x => x.Variables).MaximumLength(10_000).When(x => x.Variables is not null);
        RuleFor(x => x)
            .Must(x => x.Subject is not null || x.HtmlBody is not null
                || x.TextBody is not null || x.Variables is not null || x.IsActive is not null)
            .WithMessage("更新するフィールドを少なくとも 1 つ指定してください");
    }
}
