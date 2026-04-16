namespace McpServer.Infrastructure.Http;

public sealed class CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(correlationId) && !request.Headers.Contains("X-Correlation-Id"))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
