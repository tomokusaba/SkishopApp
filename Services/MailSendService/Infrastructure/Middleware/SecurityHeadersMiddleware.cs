namespace MailSendService.Infrastructure.Middleware;

/// <summary>
/// OWASP 推奨のセキュリティヘッダーをレスポンスに付与するミドルウェア。
/// </summary>
/// <remarks>
/// 以下のヘッダーを全レスポンスに追加する:
/// X-Content-Type-Options, X-Frame-Options, Content-Security-Policy,
/// Referrer-Policy, Permissions-Policy。
/// </remarks>
/// <param name="next">次のミドルウェアデリゲート。</param>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>MIME スニッフィング防止ヘッダー値。</summary>
    private const string ContentTypeOptions = "nosniff";
    /// <summary>クリックジャッキング防止ヘッダー値。</summary>
    private const string FrameOptions = "DENY";
    /// <summary>XSS・データインジェクション防止用 CSP ヘッダー値。</summary>
    private const string ContentSecurityPolicy = "default-src 'self'";
    /// <summary>Referer ヘッダー情報漏洩防止ポリシー。</summary>
    private const string ReferrerPolicy = "strict-origin-when-cross-origin";
    /// <summary>ブラウザ機能制限ポリシー（カメラ、マイク、位置情報を無効化）。</summary>
    private const string PermissionsPolicy = "camera=(), microphone=(), geolocation=()";

    /// <summary>
    /// レスポンスにセキュリティヘッダーを付与し、次のミドルウェアを実行する。
    /// </summary>
    /// <param name="context">HTTP コンテキスト。</param>
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Append("X-Content-Type-Options", ContentTypeOptions);
            context.Response.Headers.Append("X-Frame-Options", FrameOptions);
            context.Response.Headers.Append("Content-Security-Policy", ContentSecurityPolicy);
            context.Response.Headers.Append("Referrer-Policy", ReferrerPolicy);
            context.Response.Headers.Append("Permissions-Policy", PermissionsPolicy);
            return Task.CompletedTask;
        });

        await next(context);
    }
}

/// <summary>
/// <see cref="SecurityHeadersMiddleware"/> をパイプラインに登録するための拡張メソッド。
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    /// <summary>
    /// セキュリティヘッダーミドルウェアをアプリケーションパイプラインに追加する。
    /// </summary>
    /// <param name="builder">アプリケーションビルダー。</param>
    /// <returns>構成されたアプリケーションビルダー。</returns>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
        => builder.UseMiddleware<SecurityHeadersMiddleware>();
}
