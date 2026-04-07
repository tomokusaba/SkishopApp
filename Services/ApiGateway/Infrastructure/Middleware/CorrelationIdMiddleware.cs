using System.Text.RegularExpressions;
using Serilog.Context;

namespace ApiGateway.Infrastructure.Middleware;

/// <summary>
/// リクエスト/レスポンスに相関 ID（<c>X-Correlation-Id</c>）を付与し、
/// Serilog の <see cref="LogContext"/> に <c>CorrelationId</c> プロパティとして記録するミドルウェア。
/// マイクロサービス間の分散トレーシングで、同一リクエストチェーンのログを横断検索するために使用される。
/// </summary>
/// <remarks>
/// <para>クライアントから <c>X-Correlation-Id</c> ヘッダーが提供された場合はそれを採用するが、
/// ログインジェクション／ヘッダインジェクション防止のため以下のバリデーションを実施する:</para>
/// <list type="bullet">
///   <item><description>最大 128 文字</description></item>
///   <item><description>英数字・ハイフン・アンダースコア・ドットのみ許可</description></item>
/// </list>
/// <para>バリデーション不合格時は新規 GUID を生成する。</para>
/// </remarks>
/// <param name="next">パイプライン内の次のミドルウェア。</param>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    /// <summary>相関 ID の HTTP ヘッダー名。</summary>
    private const string HeaderName = "X-Correlation-Id";

    /// <summary>受け入れ可能な相関 ID の最大文字数。</summary>
    private const int MaxCorrelationIdLength = 128;

    /// <summary>
    /// 安全な相関 ID のパターン（英数字・ハイフン・アンダースコア・ドットのみ許可）。
    /// <see cref="GeneratedRegexAttribute"/> によりコンパイル時にソースジェネレート。
    /// </summary>
    [GeneratedRegex(@"^[a-zA-Z0-9\-_.]+$")]
    private static partial Regex SafeCorrelationIdPattern();

    /// <summary>
    /// 相関 ID のバリデーション・付与・Serilog コンテキストへの注入を行う。
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

        // クライアント提供の Correlation ID をバリデーション（ログインジェクション防止）
        if (string.IsNullOrWhiteSpace(correlationId)
            || correlationId.Length > MaxCorrelationIdLength
            || !SafeCorrelationIdPattern().IsMatch(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Items["CorrelationId"] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
