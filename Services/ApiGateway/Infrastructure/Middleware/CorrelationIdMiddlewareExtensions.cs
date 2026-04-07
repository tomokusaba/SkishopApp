namespace ApiGateway.Infrastructure.Middleware;

/// <summary>
/// <see cref="CorrelationIdMiddleware"/> をミドルウェアパイプラインに登録する拡張メソッド。
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// 相関 ID（<c>X-Correlation-Id</c>）の付与・検証・Serilog 連携を行うミドルウェアを追加する。
    /// </summary>
    /// <param name="builder">アプリケーションビルダー。</param>
    /// <returns>ミドルウェアが追加された <see cref="IApplicationBuilder"/>。</returns>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
        => builder.UseMiddleware<CorrelationIdMiddleware>();
}
