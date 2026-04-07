namespace AuthService.Infrastructure.Middleware;

/// <summary>
/// HTTP レスポンスにセキュリティ関連のヘッダーを追加するミドルウェア。
/// </summary>
/// <remarks>
/// <para>
/// このミドルウェアは、OWASP のセキュリティベストプラクティスに従い、
/// 各レスポンスにセキュリティヘッダーを追加します。これにより、
/// XSS、クリックジャッキング、MIME スニッフィングなどの攻撃を緩和します。
/// </para>
/// <para>
/// <strong>追加されるヘッダー:</strong>
/// <list type="table">
///   <listheader>
///     <term>ヘッダー</term>
///     <description>値と目的</description>
///   </listheader>
///   <item>
///     <term><c>X-Content-Type-Options</c></term>
///     <description><c>nosniff</c> - ブラウザによる MIME タイプのスニッフィングを防止</description>
///   </item>
///   <item>
///     <term><c>X-Frame-Options</c></term>
///     <description><c>DENY</c> - iframe での埋め込みを完全に禁止（クリックジャッキング対策）</description>
///   </item>
///   <item>
///     <term><c>Content-Security-Policy</c></term>
///     <description><c>default-src 'self'</c> - 同一オリジンのリソースのみを許可（XSS 対策）</description>
///   </item>
///   <item>
///     <term><c>Referrer-Policy</c></term>
///     <description><c>strict-origin-when-cross-origin</c> - クロスオリジンリクエスト時の Referer 情報を制限</description>
///   </item>
///   <item>
///     <term><c>Permissions-Policy</c></term>
///     <description><c>camera=(), microphone=(), geolocation=()</c> - 不要な機能 API へのアクセスを制限</description>
///   </item>
/// </list>
/// </para>
/// <para>
/// <strong>注意:</strong>
/// API サービス（JSON レスポンスのみ）の場合、Content-Security-Policy の設定は
/// フロントエンドほど厳密である必要はありませんが、多層防御として設定しています。
/// </para>
/// <para>
/// <strong>使用例（Program.cs）:</strong>
/// <code>
/// app.UseSecurityHeaders();
/// </code>
/// </para>
/// </remarks>
/// <param name="next">次のミドルウェアを呼び出すデリゲート。</param>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>
    /// ミドルウェアの処理を実行します。
    /// </summary>
    /// <param name="context">HTTP コンテキスト。</param>
    /// <returns>処理完了を表す非同期タスク。</returns>
    /// <remarks>
    /// <para>
    /// セキュリティヘッダーは、レスポンスボディが書き込まれる前に追加されます。
    /// これにより、すべてのレスポンス（エラーレスポンスを含む）にヘッダーが適用されます。
    /// </para>
    /// </remarks>
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
