using AuthService.DTOs.Requests;
using FluentValidation;

namespace AuthService.Validators;

/// <summary>
/// <see cref="ClientCredentialsRequest"/> のバリデーター。
/// OAuth 2.0 Client Credentials グラントリクエストのすべてのフィールドを検証する。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>ClientId: 必須、最大100文字</description></item>
///   <item><description>ClientSecret: 必須、最大255文字</description></item>
///   <item><description>Scope: 必須</description></item>
///   <item><description>GrantType: 必須、"client_credentials" のみ許可</description></item>
/// </list>
/// </remarks>
public class ClientCredentialsRequestValidator : AbstractValidator<ClientCredentialsRequest>
{
    /// <summary>
    /// <see cref="ClientCredentialsRequestValidator"/> の新しいインスタンスを初期化する。
    /// </summary>
    public ClientCredentialsRequestValidator()
    {
        RuleFor(x => x.ClientId)
            .NotEmpty().WithMessage("クライアントIDは必須です")
            .MaximumLength(100);

        RuleFor(x => x.ClientSecret)
            .NotEmpty().WithMessage("クライアントシークレットは必須です")
            .MaximumLength(255);

        RuleFor(x => x.Scope)
            .NotEmpty().WithMessage("スコープは必須です");

        RuleFor(x => x.GrantType)
            .NotEmpty().WithMessage("グラントタイプは必須です")
            .Equal("client_credentials").WithMessage("グラントタイプは 'client_credentials' である必要があります");
    }
}
