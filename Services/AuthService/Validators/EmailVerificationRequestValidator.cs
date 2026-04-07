using AuthService.DTOs.Requests;
using FluentValidation;

namespace AuthService.Validators;

/// <summary>
/// <see cref="EmailVerificationRequest"/> のバリデーター。
/// メールアドレス確認リクエストのトークンを検証する。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>Token: 必須（メールに含まれる確認トークン）</description></item>
/// </list>
/// </remarks>
public class EmailVerificationRequestValidator : AbstractValidator<EmailVerificationRequest>
{
    /// <summary>
    /// <see cref="EmailVerificationRequestValidator"/> の新しいインスタンスを初期化する。
    /// </summary>
    public EmailVerificationRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("トークンは必須です");
    }
}
