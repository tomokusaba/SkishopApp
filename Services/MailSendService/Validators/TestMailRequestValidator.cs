using FluentValidation;
using MailSendService.DTOs.Requests;

namespace MailSendService.Validators;

/// <summary>
/// <see cref="TestMailRequest"/> の FluentValidation バリデーター。
/// </summary>
/// <remarks>
/// メールアドレスの形式・長さ、テンプレート名の形式（半角英小文字・数字・ハイフンのみ）、
/// および変数の最大数（50 個）を検証する。
/// </remarks>
public class TestMailRequestValidator : AbstractValidator<TestMailRequest>
{
    /// <summary>
    /// バリデーションルールを定義するコンストラクタ。
    /// </summary>
    public TestMailRequestValidator()
    {
        RuleFor(x => x.RecipientEmail)
            .NotEmpty().WithMessage("送信先メールアドレスは必須です")
            .MaximumLength(255)
            .EmailAddress().WithMessage("有効なメールアドレスを入力してください");

        RuleFor(x => x.TemplateName)
            .NotEmpty().WithMessage("テンプレート名は必須です")
            .MaximumLength(100)
            .Matches(@"^[a-z0-9-]+$").WithMessage("テンプレート名は半角英小文字・数字・ハイフンのみ");

        RuleFor(x => x.Variables)
            .Must(v => v is null || v.Count <= 50)
            .WithMessage("変数は最大50個までです");
    }
}
