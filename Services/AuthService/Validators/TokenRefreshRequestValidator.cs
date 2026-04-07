using AuthService.DTOs.Requests;
using FluentValidation;

namespace AuthService.Validators;

/// <summary>
/// <see cref="TokenRefreshRequest"/> のバリデーター。
/// トークンリフレッシュリクエストのリフレッシュトークンを検証する。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>RefreshToken: 必須</description></item>
/// </list>
/// </remarks>
public class TokenRefreshRequestValidator : AbstractValidator<TokenRefreshRequest>
{
    /// <summary>
    /// <see cref="TokenRefreshRequestValidator"/> の新しいインスタンスを初期化する。
    /// </summary>
    public TokenRefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("リフレッシュトークンは必須です");
    }
}
