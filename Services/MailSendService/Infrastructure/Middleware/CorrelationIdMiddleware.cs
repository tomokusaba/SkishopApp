using Serilog.Context;

namespace MailSendService.Infrastructure.Middleware;

/// <summary>
/// リクエストの相関 ID（X-Correlation-Id）を管理するミドルウェア。
/// </summary>
/// <remarks>
/// <para>リクエストヘッダーに X-Correlation-Id が含まれていればその値を使用し、
/// 含まれていなければ新しい GUID を生成する。</para>
/// <para>レスポンスヘッダーにも相関 ID を付与し、Serilog の <see cref="LogContext"/> に
/// プロパティとして追加することで、全ログに相関 ID を自動的に含める。</para>
/// </remarks>
/// <param name="next">次のミドルウェアデリゲート。</param>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    /// <summary>相関 ID に使用する HTTP ヘッダー名。</summary>
    private const string HeaderName = "X-Correlation-Id";

    /// <summary>
    /// 相関 ID の抽出・生成を行い、リクエストコンテキストとログコンテキストに設定する。
    /// </summary>
    /// <param name="context">HTTP コンテキスト。</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Append(HeaderName, correlationId);
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}

/// <summary>
/// <see cref="CorrelationIdMiddleware"/> をパイプラインに登録するための拡張メソッド。
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// 相関 ID ミドルウェアをアプリケーションパイプラインに追加する。
    /// </summary>
    /// <param name="builder">アプリケーションビルダー。</param>
    /// <returns>構成されたアプリケーションビルダー。</returns>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
        => builder.UseMiddleware<CorrelationIdMiddleware>();
}
