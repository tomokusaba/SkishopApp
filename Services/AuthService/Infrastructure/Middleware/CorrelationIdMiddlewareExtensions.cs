namespace AuthService.Infrastructure.Middleware;

/// <summary>
/// <see cref="CorrelationIdMiddleware"/> をパイプラインに追加するための拡張メソッドを提供します。
/// </summary>
/// <remarks>
/// <para>
/// この拡張メソッドを使用することで、Program.cs で流暢なミドルウェア登録が可能になります。
/// </para>
/// <para>
/// <strong>使用例:</strong>
/// <code>
/// var app = builder.Build();
/// app.UseCorrelationId();  // 相関 ID ミドルウェアを追加
/// app.UseSerilogRequestLogging();
/// // ... 他のミドルウェア
/// </code>
/// </para>
/// <para>
/// <strong>推奨される登録順序:</strong>
/// <list type="number">
///   <item><description>UseExceptionHandler（例外ハンドラー）</description></item>
///   <item><description>UseSecurityHeaders（セキュリティヘッダー）</description></item>
///   <item><description><strong>UseCorrelationId</strong>（相関 ID）← ログ記録より前に配置</description></item>
///   <item><description>UseSerilogRequestLogging（リクエストログ）</description></item>
///   <item><description>UseAuthentication / UseAuthorization</description></item>
/// </list>
/// </para>
/// </remarks>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// アプリケーションパイプラインに <see cref="CorrelationIdMiddleware"/> を追加します。
    /// </summary>
    /// <param name="builder">アプリケーションビルダー。</param>
    /// <returns>ミドルウェアが追加されたアプリケーションビルダー。</returns>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
        => builder.UseMiddleware<CorrelationIdMiddleware>();
}
