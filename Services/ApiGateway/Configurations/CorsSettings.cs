// ─────────────────────────────────────────────────────────────
// CorsSettings — CORS 設定クラス
// ValidateOnStart() で起動時に AllowedOrigins の存在を検証し、
// 意図しない全オリジン拒否を防止する。
// ─────────────────────────────────────────────────────────────

using System.ComponentModel.DataAnnotations;

namespace ApiGateway.Configurations;

/// <summary>
/// CORS（Cross-Origin Resource Sharing）の設定値。
/// <c>ValidateOnStart()</c> により、許可オリジンが設定されていない状態での
/// アプリケーション起動を防止する。
/// </summary>
/// <remarks>
/// <para>セキュリティ上、ワイルドカード（<c>*</c>）は禁止する。</para>
/// <para>環境ごとに <c>appsettings.{Environment}.json</c> で適切なオリジンを設定すること。</para>
/// </remarks>
public record CorsSettings
{
    /// <summary>
    /// 許可するオリジンの一覧。最低 1 つのオリジンが必須。
    /// </summary>
    /// <example>
    /// <code>["https://www.skieshop.com", "https://admin.skieshop.com"]</code>
    /// </example>
    [Required(ErrorMessage = "Cors:AllowedOrigins は必須です")]
    [MinLength(1, ErrorMessage = "Cors:AllowedOrigins には少なくとも 1 つのオリジンを設定してください")]
    public string[] AllowedOrigins { get; init; } = [];
}
