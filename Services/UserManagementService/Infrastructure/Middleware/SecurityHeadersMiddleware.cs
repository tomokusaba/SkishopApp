namespace UserManagementService.Infrastructure.Middleware;

/// <summary>
/// セキュリティヘッダーをレスポンスに追加するミドルウェア。
/// X-Content-Type-Options, X-Frame-Options, CSP, Referrer-Policy, Permissions-Policy を設定する。
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
        await next(context);
    }
}
