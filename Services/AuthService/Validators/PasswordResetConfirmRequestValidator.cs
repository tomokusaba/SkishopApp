using AuthService.DTOs.Requests;
using FluentValidation;

namespace AuthService.Validators;

/// <summary>
/// <see cref="PasswordResetConfirmRequest"/> のバリデーター。
/// パスワードリセット確認リクエストのトークンと新しいパスワードを検証する。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>Token: 必須（パスワードリセットメールに含まれるトークン）</description></item>
///   <item><description>NewPassword: 必須、8〜100文字、大文字・小文字・数字・特殊文字を各1文字以上含む</description></item>
/// </list>
/// </remarks>
public class PasswordResetConfirmRequestValidator : AbstractValidator<PasswordResetConfirmRequest>
{
    /// <summary>
    /// <see cref="PasswordResetConfirmRequestValidator"/> の新しいインスタンスを初期化する。
    /// </summary>
    public PasswordResetConfirmRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("トークンは必須です");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("新しいパスワードは必須です")
            .MinimumLength(8).WithMessage("パスワードは8文字以上必要です")
            .MaximumLength(100)
            .Matches(@"[A-Z]").WithMessage("パスワードには大文字を1文字以上含めてください")
            .Matches(@"[a-z]").WithMessage("パスワードには小文字を1文字以上含めてください")
            .Matches(@"[0-9]").WithMessage("パスワードには数字を1文字以上含めてください")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("パスワードには特殊文字を1文字以上含めてください");
    }
}
