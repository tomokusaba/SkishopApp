using AuthService.DTOs.Requests;
using FluentValidation;

namespace AuthService.Validators;

/// <summary>
/// <see cref="PasswordChangeRequest"/> のバリデーター。
/// パスワード変更リクエストの現在のパスワードと新しいパスワードを検証する。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>CurrentPassword: 必須</description></item>
///   <item><description>NewPassword: 必須、8〜100文字、大文字・小文字・数字・特殊文字を各1文字以上含む</description></item>
///   <item><description>NewPassword は CurrentPassword と異なる値である必要がある</description></item>
/// </list>
/// </remarks>
public class PasswordChangeRequestValidator : AbstractValidator<PasswordChangeRequest>
{
    /// <summary>
    /// <see cref="PasswordChangeRequestValidator"/> の新しいインスタンスを初期化する。
    /// </summary>
    public PasswordChangeRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("現在のパスワードは必須です");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("新しいパスワードは必須です")
            .MinimumLength(8).WithMessage("パスワードは8文字以上必要です")
            .MaximumLength(100)
            .Matches(@"[A-Z]").WithMessage("パスワードには大文字を1文字以上含めてください")
            .Matches(@"[a-z]").WithMessage("パスワードには小文字を1文字以上含めてください")
            .Matches(@"[0-9]").WithMessage("パスワードには数字を1文字以上含めてください")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("パスワードには特殊文字を1文字以上含めてください")
            .NotEqual(x => x.CurrentPassword).WithMessage("新しいパスワードは現在のパスワードと異なる必要があります");
    }
}
