namespace AuthService.Infrastructure.Middleware;

/// <summary>
/// <see cref="SecurityHeadersMiddleware"/> をパイプラインに追加するための拡張メソッドを提供します。
/// </summary>
/// <remarks>
/// <para>
/// この拡張メソッドを使用することで、Program.cs で流暢なミドルウェア登録が可能になります。
/// </para>
/// <para>
/// <strong>使用例:</strong>
/// <code>
/// var app = builder.Build();
/// app.UseExceptionHandler();
/// app.UseHsts();
/// app.UseSecurityHeaders();  // セキュリティヘッダーミドルウェアを追加
/// // ... 他のミドルウェア
/// </code>
/// </para>
/// <para>
/// <strong>推奨される登録順序:</strong>
/// <list type="number">
///   <item><description>UseExceptionHandler（例外ハンドラー）</description></item>
///   <item><description>UseHsts（HTTP Strict Transport Security）</description></item>
///   <item><description>UseHttpsRedirection（HTTPS リダイレクト）</description></item>
///   <item><description><strong>UseSecurityHeaders</strong>（セキュリティヘッダー）</description></item>
///   <item><description>UseCorrelationId（相関 ID）</description></item>
///   <item><description>UseSerilogRequestLogging（リクエストログ）</description></item>
/// </list>
/// </para>
/// </remarks>
public static class SecurityHeadersMiddlewareExtensions
{
    /// <summary>
    /// アプリケーションパイプラインに <see cref="SecurityHeadersMiddleware"/> を追加します。
    /// </summary>
    /// <param name="builder">アプリケーションビルダー。</param>
    /// <returns>ミドルウェアが追加されたアプリケーションビルダー。</returns>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
        => builder.UseMiddleware<SecurityHeadersMiddleware>();
}
