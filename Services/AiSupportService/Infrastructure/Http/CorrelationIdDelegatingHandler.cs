namespace AiSupportService.Infrastructure.Http;

/// <summary>
/// 外部 HTTP リクエストに相関 ID（Correlation ID）を伝搬する <see cref="DelegatingHandler"/>。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IHttpClientFactory"/> 経由で送信する全ての HTTP リクエストに対して、
/// 受信リクエストの <c>X-Correlation-Id</c> ヘッダー（またはフォールバックとして <c>TraceIdentifier</c>）を
/// 送信リクエストヘッダーにコピーする。これにより、マイクロサービス間の分散トレーシングが可能になる。
/// </para>
/// <para>
/// <b>分散トレーシング:</b> OpenTelemetry と組み合わせることで、相関 ID を基にしたリクエストフロー全体の
/// 追跡が可能になる。ログ集約基盤（Elasticsearch, Loki 等）での横断検索にも活用できる。
/// </para>
/// <para>
/// <b>フォールバック動作:</b> 受信リクエストに <c>X-Correlation-Id</c> ヘッダーが存在しない場合、
/// ASP.NET Core が自動生成する <c>HttpContext.TraceIdentifier</c> を使用する。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Program.cs での HttpClient 登録
/// builder.Services.AddTransient&lt;CorrelationIdDelegatingHandler&gt;();
/// builder.Services.AddHttpClient&lt;IInventoryClient, InventoryClient&gt;()
///     .AddHttpMessageHandler&lt;CorrelationIdDelegatingHandler&gt;();
/// </code>
/// </example>
/// <seealso cref="CorrelationIdMiddleware"/>
/// <param name="httpContextAccessor">
/// 現在の HTTP コンテキストへのアクセサ。受信リクエストの相関 ID を取得するために使用する。
/// </param>
public class CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor)
    : DelegatingHandler
{
    /// <summary>
    /// HTTP リクエスト送信前に相関 ID ヘッダーを付与し、リクエストを転送する。
    /// </summary>
    /// <param name="request">
    /// 送信対象の HTTP リクエストメッセージ。<c>X-Correlation-Id</c> ヘッダーが追加される。
    /// </param>
    /// <param name="cancellationToken">
    /// キャンセルトークン。下位の HTTP クライアントに伝搬される。
    /// </param>
    /// <returns>
    /// 下位ハンドラーから返された HTTP レスポンスメッセージ。
    /// </returns>
    /// <remarks>
    /// <para>
    /// <see cref="IHttpContextAccessor.HttpContext"/> が <c>null</c> の場合（例: バックグラウンドサービスからの呼び出し）、
    /// 相関 ID ヘッダーは追加されない。この場合、呼び出し元で明示的に相関 ID を設定することを推奨する。
    /// </para>
    /// <para>
    /// <b>ヘッダー重複防止:</b> <see cref="HttpRequestHeaders.TryAddWithoutValidation"/> を使用しているため、
    /// 既に同名ヘッダーが存在する場合は追加されない。
    /// </para>
    /// </remarks>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            var correlationId = httpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                ?? httpContext.TraceIdentifier;

            if (!string.IsNullOrEmpty(correlationId))
                request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
