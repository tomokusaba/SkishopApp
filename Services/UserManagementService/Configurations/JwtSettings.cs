using System.ComponentModel.DataAnnotations;

namespace UserManagementService.Configurations;

/// <summary>
/// JWT 認証設定。AuthService と同一の SigningKey を共有する必要がある。
/// <c>appsettings.json</c> の <c>Jwt</c> セクションにバインドされる。
/// </summary>
public record JwtSettings
{
    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;
}
