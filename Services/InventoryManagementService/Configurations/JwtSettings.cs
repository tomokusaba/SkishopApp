using System.ComponentModel.DataAnnotations;

namespace InventoryManagementService.Configurations;

/// <summary>
/// JWT 認証設定。appsettings.json の "Jwt" セクションにバインドされる。
/// IOptions<T> でのバインディングに対応するため、init プロパティを使用。
/// </summary>
public record JwtSettings
{
    /// <summary>トークン発行者（iss クレーム）</summary>
    [Required]
    public string Issuer { get; init; } = string.Empty;

    /// <summary>トークン受信者（aud クレーム）</summary>
    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>HMAC-SHA256 署名鍵（環境変数または user-secrets で管理）</summary>
    [Required, MinLength(32)]
    public string SecretKey { get; init; } = string.Empty;
}
