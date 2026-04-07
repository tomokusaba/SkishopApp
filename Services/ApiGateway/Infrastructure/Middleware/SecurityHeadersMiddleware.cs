using Microsoft.Extensions.Primitives;

namespace ApiGateway.Infrastructure.Middleware;

/// <summary>
/// OWASP 推奨のセキュリティヘッダーを全 HTTP レスポンスに付与するミドルウェア。
/// <list type="bullet">
///   <item><description><c>X-Content-Type-Options: nosniff</c> — MIME スニッフィング防止</description></item>
///   <item><description><c>X-Frame-Options: DENY</c> — クリックジャッキング防止</description></item>
///   <item><description><c>Content-Security-Policy: default-src 'self'</c> — XSS/データインジェクション防止</description></item>
///   <item><description><c>Strict-Transport-Security</c> — HTTPS 強制（HSTS）</description></item>
///   <item><description><c>Referrer-Policy</c> — Referer ヘッダー情報漏洩防止</description></item>
///   <item><description><c>Permissions-Policy</c> — 不要なブラウザ機能の無効化</description></item>
///   <item><description><c>Cache-Control: no-store</c> — API レスポンスのキャッシュ禁止</description></item>
/// </list>
/// ヘッダー値は <see cref="StringValues"/> として事前に確保し、リクエスト毎のアロケーションを回避する。
/// </summary>
/// <param name="next">パイプライン内の次のミドルウェア。</param>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private static readonly StringValues NoSniff = new("nosniff");
    private static readonly StringValues Deny = new("DENY");
    private static readonly StringValues Csp = new("default-src 'self'");
    private static readonly StringValues Hsts = new("max-age=31536000; includeSubDomains");
    private static readonly StringValues XssProtection = new("0");
    private static readonly StringValues ReferrerPolicy = new("strict-origin-when-cross-origin");
    private static readonly StringValues PermissionsPolicy = new("camera=(), microphone=(), geolocation=()");
    private static readonly StringValues CacheControl = new("no-store");
    private static readonly StringValues Pragma = new("no-cache");

    /// <summary>
    /// レスポンス送信開始時にセキュリティヘッダーを付与し、<c>Server</c> ヘッダーを除去する。
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = NoSniff;
            headers["X-Frame-Options"] = Deny;
            headers["Content-Security-Policy"] = Csp;
            headers["Strict-Transport-Security"] = Hsts;
            headers["X-XSS-Protection"] = XssProtection;
            headers["Referrer-Policy"] = ReferrerPolicy;
            headers["Permissions-Policy"] = PermissionsPolicy;
            headers["Cache-Control"] = CacheControl;
            headers["Pragma"] = Pragma;
            headers.Remove("Server");

            return Task.CompletedTask;
        });

        await next(context);
    }
}
