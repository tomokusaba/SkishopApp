namespace InventoryManagementService.Infrastructure.Http;

/// <summary>
/// 相関ID伝搬用の DelegatingHandler。
/// 受信リクエストの相関IDを、外部 HTTP 呼び出しの X-Correlation-Id ヘッダーに転送する。
/// </summary>
/// <param name="httpContextAccessor">HTTPコンテキストアクセサー</param>
/// <remarks>
/// HttpContext.Items["CorrelationId"] から相関IDを読み取り、
/// IHttpClientFactory 経由の全送信 HTTP リクエストに自動的に付与する。
/// CorrelationIdMiddleware と連携して分散トレーシングを実現する。
/// </remarks>
public class CorrelationIdDelegatingHandler(
    IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    /// <summary>
    /// HTTP リクエスト送信前に X-Correlation-Id ヘッダーを付与する。
    /// </summary>
    /// <param name="request">送信する HTTP リクエスト</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>HTTP レスポンス</returns>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = httpContextAccessor.HttpContext?
            .Items["CorrelationId"]?.ToString();
        if (!string.IsNullOrEmpty(correlationId))
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);

        return await base.SendAsync(request, cancellationToken);
    }
}
