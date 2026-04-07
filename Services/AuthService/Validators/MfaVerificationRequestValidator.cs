using AuthService.DTOs.Requests;
using FluentValidation;

namespace AuthService.Validators;

/// <summary>
/// <see cref="MfaVerificationRequest"/> のバリデーター。
/// MFA 検証リクエストの認証コードとセッショントークンを検証する。
/// </summary>
/// <remarks>
/// <para>バリデーションルール:</para>
/// <list type="bullet">
///   <item><description>Code: 必須、6桁の数字（TOTP コード）</description></item>
///   <item><description>SessionToken: 必須（ログイン時に発行された一時セッショントークン）</description></item>
/// </list>
/// </remarks>
public class MfaVerificationRequestValidator : AbstractValidator<MfaVerificationRequest>
{
    /// <summary>
    /// <see cref="MfaVerificationRequestValidator"/> の新しいインスタンスを初期化する。
    /// </summary>
    public MfaVerificationRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("認証コードは必須です")
            .Length(6).WithMessage("認証コードは6桁で入力してください")
            .Matches(@"^\d{6}$").WithMessage("認証コードは数字6桁で入力してください");

        RuleFor(x => x.SessionToken)
            .NotEmpty().WithMessage("セッショントークンは必須です");
    }
}
