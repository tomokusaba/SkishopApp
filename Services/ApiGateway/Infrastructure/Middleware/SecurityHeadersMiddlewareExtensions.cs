namespace ApiGateway.Infrastructure.Middleware;

/// <summary>
/// <see cref="SecurityHeadersMiddleware"/> をミドルウェアパイプラインに登録する拡張メソッド。
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    /// <summary>
    /// OWASP 推奨のセキュリティヘッダーを全レスポンスに付与するミドルウェアを追加する。
    /// </summary>
    /// <param name="builder">アプリケーションビルダー。</param>
    /// <returns>ミドルウェアが追加された <see cref="IApplicationBuilder"/>。</returns>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
        => builder.UseMiddleware<SecurityHeadersMiddleware>();
}
