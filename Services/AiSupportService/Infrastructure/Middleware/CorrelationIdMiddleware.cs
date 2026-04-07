using Serilog.Context;

namespace AiSupportService.Infrastructure.Middleware;

/// <summary>
/// リクエストに相関 ID（Correlation ID）を付与する ASP.NET Core ミドルウェア。
/// </summary>
/// <remarks>
/// <para>
/// 分散システムにおけるリクエストトレーシングを実現するため、
/// 全 HTTP リクエストに一意の相関 ID を付与する。この ID はログ出力・レスポンスヘッダー・
/// 下流サービスへの伝搬に使用され、エンドツーエンドのリクエスト追跡を可能にする。
/// </para>
/// <para>
/// <b>相関 ID の決定ロジック:</b>
/// <list type="number">
///   <item><description>リクエストヘッダー <c>X-Correlation-Id</c> が存在する場合: その値を使用</description></item>
///   <item><description>存在しない場合: 新規 GUID を生成</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Serilog 統合:</b> <see cref="LogContext.PushProperty"/> により、
/// リクエストスコープ内の全ログに <c>CorrelationId</c> プロパティが自動付与される。
/// ログ集約基盤での横断検索に活用できる。
/// </para>
/// <para>
/// <b>ミドルウェア順序:</b> 認証・認可ミドルウェアより前（できるだけパイプラインの先頭）に配置し、
/// 全てのログに相関 ID が含まれるようにする。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Program.cs での登録
/// app.UseCorrelationId();  // UseAuthentication() より前に配置
/// app.UseAuthentication();
/// app.UseAuthorization();
/// </code>
/// </example>
/// <seealso cref="CorrelationIdDelegatingHandler"/>
/// <param name="next">パイプライン内の次のミドルウェアへのデリゲート。</param>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    /// <summary>
    /// 相関 ID の取得・生成を行い、ログコンテキストに設定した上で次のミドルウェアを実行する。
    /// </summary>
    /// <param name="context">現在の HTTP コンテキスト。</param>
    /// <returns>非同期処理を表すタスク。</returns>
    /// <remarks>
    /// <para>
    /// <b>レスポンスヘッダー:</b> <c>X-Correlation-Id</c> ヘッダーをレスポンスにも設定するため、
    /// クライアントは自身のリクエストに対応する相関 ID を確認できる。
    /// </para>
    /// <para>
    /// <b>using ブロック:</b> <see cref="LogContext.PushProperty"/> は <c>IDisposable</c> を返し、
    /// <c>using</c> ブロックを抜けると LogContext からプロパティが自動的に除去される。
    /// これによりリクエストスコープの分離が保証される。
    /// </para>
    /// </remarks>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();
        context.Response.Headers.Append("X-Correlation-Id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}

/// <summary>
/// <see cref="CorrelationIdMiddleware"/> を <see cref="IApplicationBuilder"/> に登録する拡張メソッド。
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// 相関 ID ミドルウェアをパイプラインに追加する。
    /// </summary>
    /// <param name="app">アプリケーションビルダー。</param>
    /// <returns>ミドルウェアが追加されたアプリケーションビルダー（メソッドチェイン用）。</returns>
    /// <remarks>
    /// 認証・認可ミドルウェアより前に配置することを推奨。
    /// </remarks>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
