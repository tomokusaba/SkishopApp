using System.ComponentModel.DataAnnotations;

namespace MailSendService.Configurations;

/// <summary>
/// JWT (JSON Web Token) 認証設定。appsettings.json の "Jwt" セクションにバインドされる。
/// </summary>
public record JwtSettings
{
    /// <summary>JWT 発行者（iss クレーム）。</summary>
    [Required(ErrorMessage = "JWT Issuer は必須です")]
    public string Issuer { get; init; } = string.Empty;

    /// <summary>JWT 対象者（aud クレーム）。</summary>
    [Required(ErrorMessage = "JWT Audience は必須です")]
    public string Audience { get; init; } = string.Empty;

    /// <summary>JWT 署名キー。HMAC-SHA256 には最低 32 文字（256 ビット）が必要。</summary>
    [Required(ErrorMessage = "JWT SecretKey は必須です")]
    [MinLength(32, ErrorMessage = "JWT SecretKey は最低 32 文字必要です（HMAC-SHA256 の要件）")]
    public string SecretKey { get; init; } = string.Empty;
}
