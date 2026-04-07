using AuthService.DTOs.Requests;
using FluentValidation;

namespace AuthService.Validators;

/// <summary>
/// <see cref="PasswordResetRequest"/> のバリデーター。
/// パスワードリセット要求リクエストのメールアドレスを検証する。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>Email: 必須、有効なメールアドレス形式</description></item>
/// </list>
/// </remarks>
public class PasswordResetRequestValidator : AbstractValidator<PasswordResetRequest>
{
    /// <summary>
    /// <see cref="PasswordResetRequestValidator"/> の新しいインスタンスを初期化する。
    /// </summary>
    public PasswordResetRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("メールアドレスは必須です")
            .EmailAddress().WithMessage("有効なメールアドレスを入力してください");
    }
}
