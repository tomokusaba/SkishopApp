using Serilog.Context;

namespace AuthService.Infrastructure.Middleware;

/// <summary>
/// リクエスト間で相関 ID を伝播させるミドルウェア。
/// </summary>
/// <remarks>
/// <para>
/// このミドルウェアは、分散トレーシングと障害分析を支援するため、
/// 各リクエストに一意の相関 ID を付与します。相関 ID は、
/// マイクロサービス間のリクエストを追跡するために使用されます。
/// </para>
/// <para>
/// <strong>相関 ID の取得ロジック:</strong>
/// <list type="number">
///   <item><description>リクエストヘッダー <c>X-Correlation-Id</c> から取得を試みます</description></item>
///   <item><description>ヘッダーが存在しない場合は、新しい GUID を生成します</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>相関 ID の使用:</strong>
/// <list type="bullet">
///   <item>レスポンスヘッダーに <c>X-Correlation-Id</c> として追加</item>
///   <item>Serilog の LogContext に <c>CorrelationId</c> プロパティとして追加</item>
/// </list>
/// </para>
/// <para>
/// <strong>セキュリティ考慮事項:</strong>
/// 外部から渡された相関 ID はそのまま使用されます。信頼できないソースからの
/// リクエストの場合、相関 ID を上書きする設定を検討してください。
/// </para>
/// <para>
/// <strong>使用例（Program.cs）:</strong>
/// <code>
/// app.UseCorrelationId();
/// </code>
/// </para>
/// </remarks>
/// <param name="next">次のミドルウェアを呼び出すデリゲート。</param>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    /// <summary>
    /// 相関 ID のヘッダー名。
    /// </summary>
    private const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>
    /// ミドルウェアの処理を実行します。
    /// </summary>
    /// <param name="context">HTTP コンテキスト。</param>
    /// <returns>処理完了を表す非同期タスク。</returns>
    /// <remarks>
    /// <para>
    /// 相関 ID は <see cref="LogContext.PushProperty"/> を使用して Serilog のコンテキストに追加されます。
    /// これにより、このリクエスト内のすべてのログメッセージに相関 ID が含まれます。
    /// </para>
    /// </remarks>
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
