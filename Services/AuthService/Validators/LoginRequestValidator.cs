using AuthService.DTOs.Requests;
using FluentValidation;

namespace AuthService.Validators;

/// <summary>
/// <see cref="LoginRequest"/> のバリデーター。
/// ログインリクエストのメールアドレスとパスワードを検証する。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>Email: 必須、有効なメールアドレス形式、最大255文字</description></item>
///   <item><description>Password: 必須、8〜100文字</description></item>
/// </list>
/// </remarks>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>
    /// <see cref="LoginRequestValidator"/> の新しいインスタンスを初期化する。
    /// </summary>
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("メールアドレスは必須です")
            .EmailAddress().WithMessage("有効なメールアドレスを入力してください")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("パスワードは必須です")
            .MinimumLength(8).WithMessage("パスワードは8文字以上必要です")
            .MaximumLength(100);
    }
}
