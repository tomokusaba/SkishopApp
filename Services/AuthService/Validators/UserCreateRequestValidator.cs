using AuthService.DTOs.Requests;
using FluentValidation;

namespace AuthService.Validators;

/// <summary>
/// <see cref="UserCreateRequest"/> のバリデーター。
/// ユーザー登録リクエストの全フィールドを包括的に検証する。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>Email: 必須、有効なメールアドレス形式、最大255文字</description></item>
///   <item><description>Username: 必須、3〜100文字、英数字・ハイフン・アンダースコアのみ</description></item>
///   <item><description>Password: 必須、8〜100文字、大文字・小文字・数字・特殊文字を各1文字以上含む</description></item>
///   <item><description>FirstName: 任意、最大100文字</description></item>
///   <item><description>LastName: 任意、最大100文字</description></item>
/// </list>
/// </remarks>
public class UserCreateRequestValidator : AbstractValidator<UserCreateRequest>
{
    /// <summary>
    /// <see cref="UserCreateRequestValidator"/> の新しいインスタンスを初期化する。
    /// </summary>
    public UserCreateRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("メールアドレスは必須です")
            .EmailAddress().WithMessage("有効なメールアドレスを入力してください")
            .MaximumLength(255);

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("ユーザー名は必須です")
            .MinimumLength(3).WithMessage("ユーザー名は3文字以上必要です")
            .MaximumLength(100)
            .Matches(@"^[a-zA-Z0-9_-]+$").WithMessage("ユーザー名は英数字・ハイフン・アンダースコアのみ使用可能です");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("パスワードは必須です")
            .MinimumLength(8).WithMessage("パスワードは8文字以上必要です")
            .MaximumLength(100)
            .Matches(@"[A-Z]").WithMessage("パスワードには大文字を1文字以上含めてください")
            .Matches(@"[a-z]").WithMessage("パスワードには小文字を1文字以上含めてください")
            .Matches(@"[0-9]").WithMessage("パスワードには数字を1文字以上含めてください")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("パスワードには特殊文字を1文字以上含めてください");

        RuleFor(x => x.FirstName)
            .MaximumLength(100)
            .When(x => x.FirstName is not null);

        RuleFor(x => x.LastName)
            .MaximumLength(100)
            .When(x => x.LastName is not null);
    }
}
