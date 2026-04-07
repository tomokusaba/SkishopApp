using System.ComponentModel.DataAnnotations;

namespace InventoryManagementService.Configurations;

/// <summary>
/// CORS（Cross-Origin Resource Sharing）設定。appsettings.json の "Cors" セクションにバインドされる。
/// フロントエンドアプリケーションからの API アクセスを許可するオリジンを定義する。
/// </summary>
public record CorsConfig
{
    /// <summary>アクセスを許可するオリジン URL の一覧。ワイルドカード "*" は禁止。</summary>
    [Required]
    [MinLength(1)]
    public string[] AllowedOrigins { get; init; } = [];
}
