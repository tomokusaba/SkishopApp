namespace Frontend.Handlers;

/// <summary>
/// レスポンスから Correlation ID を取得し、エラーログに記録する DelegatingHandler
/// §16.3 / §4.4 準拠 — API Gateway が X-Correlation-Id を自動生成
/// </summary>
public class CorrelationIdHandler(ILogger<CorrelationIdHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.Headers.TryGetValues("X-Correlation-Id", out var values))
        {
            var correlationId = values.FirstOrDefault();
            if (!response.IsSuccessStatusCode && correlationId is not null)
            {
                logger.LogWarning("API エラー [Correlation-Id: {CorrelationId}] Status: {StatusCode}",
                    correlationId, (int)response.StatusCode);
            }
        }

        return response;
    }
}
