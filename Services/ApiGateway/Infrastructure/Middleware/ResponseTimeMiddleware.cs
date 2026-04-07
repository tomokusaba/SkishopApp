using System.Diagnostics;
using ApiGateway.Infrastructure.Metrics;

namespace ApiGateway.Infrastructure.Middleware;

/// <summary>
/// リクエスト処理時間を計測し、<c>X-Response-Time</c> ヘッダーとして付与するミドルウェア。
/// 同時に <see cref="GatewayMetrics.RequestsTotal"/> および <see cref="GatewayMetrics.RequestDuration"/>
/// メトリクスを記録し、OpenTelemetry 経由で監視ダッシュボードに送信する。
/// </summary>
/// <remarks>
/// <see cref="Stopwatch.GetTimestamp"/> / <see cref="Stopwatch.GetElapsedTime"/> を使用し、
/// <see cref="Stopwatch"/> インスタンスのアロケーションを回避した高精度計測を行う。
/// </remarks>
/// <param name="next">パイプライン内の次のミドルウェア。</param>
/// <param name="metrics">メトリクス記録用の DI インスタンス。</param>
public sealed class ResponseTimeMiddleware(RequestDelegate next, GatewayMetrics metrics)
{
    /// <summary>
    /// リクエスト開始時のタイムスタンプを取得し、レスポンス送信開始時に経過時間を計算する。
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = Stopwatch.GetTimestamp();
        context.Response.OnStarting(() =>
        {
            var elapsed = Stopwatch.GetElapsedTime(startTime);
            context.Response.Headers.Append("X-Response-Time", $"{elapsed.TotalMilliseconds:F1}ms");

            // OpenTelemetry メトリクス記録
            metrics.RequestsTotal.Add(1,
                new KeyValuePair<string, object?>("method", context.Request.Method),
                new KeyValuePair<string, object?>("status", context.Response.StatusCode));
            metrics.RequestDuration.Record(elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("method", context.Request.Method),
                new KeyValuePair<string, object?>("path", context.Request.Path.Value));

            return Task.CompletedTask;
        });
        await next(context);
    }
}
