using Serilog.Context;

namespace UserManagementService.Infrastructure.Middleware;

/// <summary>
/// リクエストに相関 ID を付与し、Serilog LogContext にプッシュするミドルウェア。
/// <c>X-Correlation-Id</c> ヘッダーがない場合は新規 GUID を生成する。
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString();
        context.Response.Headers.Append(CorrelationIdHeader, correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
