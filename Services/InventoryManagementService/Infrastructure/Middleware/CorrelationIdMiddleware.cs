using Serilog.Context;

namespace InventoryManagementService.Infrastructure.Middleware;

/// <summary>
/// 相関ID（Correlation ID）ミドルウェア。
/// リクエストヘッダー X-Correlation-Id を伝搬、または新規生成してレスポンスと HttpContext.Items に格納する。
/// </summary>
/// <param name="next">次のミドルウェアデリゲート</param>
/// <remarks>
/// ミドルウェアパイプラインの最初に配置すること（ExceptionHandler より前）。
/// HttpContext.Items["CorrelationId"] に格納された値は、GlobalExceptionHandler や
/// CorrelationIdDelegatingHandler から参照される。
/// Serilog の LogContext にも CorrelationId を Push し、全ログに相関IDを付与する。
/// </remarks>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    /// <summary>
    /// ミドルウェア処理を実行する。
    /// </summary>
    /// <param name="context">HTTPコンテキスト</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();
        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers.Append("X-Correlation-Id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}

/// <summary>
/// CorrelationIdMiddleware の IApplicationBuilder 拡張メソッドクラス。
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// 相関IDミドルウェアをパイプラインに追加する。
    /// </summary>
    /// <param name="builder">アプリケーションビルダー</param>
    /// <returns>ミドルウェアが追加されたアプリケーションビルダー</returns>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
        => builder.UseMiddleware<CorrelationIdMiddleware>();
}
