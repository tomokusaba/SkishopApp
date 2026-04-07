namespace InventoryManagementService.Infrastructure.Middleware;

/// <summary>
/// セキュリティヘッダーミドルウェア。
/// 全レスポンスにセキュリティ関連の HTTP ヘッダーを付与する。
/// </summary>
/// <param name="next">次のミドルウェアデリゲート</param>
/// <remarks>
/// 付与するヘッダー:
/// - X-Content-Type-Options: nosniff（MIME スニッフィング防止）
/// - X-Frame-Options: DENY（クリックジャッキング防止）
/// - Content-Security-Policy: default-src 'self'（XSS / データインジェクション防止）
/// - Referrer-Policy: strict-origin-when-cross-origin（Referer 情報漏洩防止）
/// - Permissions-Policy: camera=(), microphone=(), geolocation=()（不要なブラウザ機能制限）
/// </remarks>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>
    /// ミドルウェア処理を実行し、セキュリティヘッダーをレスポンスに追加する。
    /// </summary>
    /// <param name="context">HTTPコンテキスト</param>
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Permissions-Policy",
            "camera=(), microphone=(), geolocation=()");
        await next(context);
    }
}

/// <summary>
/// SecurityHeadersMiddleware の IApplicationBuilder 拡張メソッドクラス。
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    /// <summary>
    /// セキュリティヘッダーミドルウェアをパイプラインに追加する。
    /// </summary>
    /// <param name="builder">アプリケーションビルダー</param>
    /// <returns>ミドルウェアが追加されたアプリケーションビルダー</returns>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
        => builder.UseMiddleware<SecurityHeadersMiddleware>();
}
