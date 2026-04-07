// ─────────────────────────────────────────────────────────────
// JwtSettings — JWT 認証設定クラス
// ValidateOnStart() で起動時に必須項目の存在を検証し、
// 設定漏れによる本番障害を防止する。
// ─────────────────────────────────────────────────────────────

using System.ComponentModel.DataAnnotations;

namespace ApiGateway.Configurations;

/// <summary>
/// JWT 認証に必要な設定値。
/// <see cref="Microsoft.Extensions.Options.OptionsBuilderDataAnnotationsExtensions.ValidateDataAnnotations"/>
/// と <see cref="JwtSettingsValidator"/> による起動時検証で、
/// 本番環境での設定漏れを即座に検出する。
/// </summary>
/// <remarks>
/// <para>鍵の種類に応じてアルゴリズムが自動選択される:</para>
/// <list type="bullet">
///   <item><see cref="SigningKey"/> 設定時 → HS256（対称鍵）</item>
///   <item><see cref="Authority"/> 設定時 → RS256/ES256（JWKS 自動取得）</item>
/// </list>
/// <para>両方が設定された場合、Authority が優先される。</para>
/// </remarks>
public record JwtSettings
{
    /// <summary>トークン発行者（iss クレーム）。必須。</summary>
    [Required(ErrorMessage = "Jwt:Issuer は必須です")]
    [MinLength(1, ErrorMessage = "Jwt:Issuer は空文字列にできません")]
    public string Issuer { get; init; } = string.Empty;

    /// <summary>トークン受信者（aud クレーム）。必須。</summary>
    [Required(ErrorMessage = "Jwt:Audience は必須です")]
    [MinLength(1, ErrorMessage = "Jwt:Audience は空文字列にできません")]
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// 対称鍵（HS256 用）。Authority 未設定時は必須。
    /// 値は環境変数または <c>dotnet user-secrets</c> で管理する（ハードコード禁止）。
    /// </summary>
    public string? SigningKey { get; init; }

    /// <summary>
    /// OpenID Connect Discovery エンドポイント（RS256/ES256 使用時）。
    /// 設定時は JWKS による公開鍵自動取得が有効になり、SigningKey は無視される。
    /// </summary>
    public string? Authority { get; init; }
}
