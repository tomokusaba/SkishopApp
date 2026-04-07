using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;
using PaymentCartService.Exceptions;

namespace PaymentCartService.Infrastructure;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (statusCode, message, errorCode) = exception switch
        {
            NotFoundException e => (404, e.Message, (string?)null),
            CartExpiredException e => (409, e.Message, e.ErrorCode),
            ConcurrencyException e => (409, e.Message, (string?)null),
            PaymentProcessingException e => (422, e.Message, e.ErrorCode),
            RefundProcessingException e => (422, e.Message, e.ErrorCode),
            StripeApiException e => (422, e.Message, e.ErrorCode),
            BusinessException e => (422, e.Message, e.ErrorCode),
            ExternalServiceException e => (503, e.Message, e.ErrorCode),
            Stripe.StripeException => (503, "決済ゲートウェイエラー", "PAY-5002"),
            _ => (500, "内部エラーが発生しました", "PAY-5001")
        };

        // ログ出力（503 は外部サービス障害のため詳細情報を記録）
        LogException(httpContext, exception, statusCode, errorCode);

        var problem = TypedResults.Problem(
            detail: message,
            statusCode: statusCode,
            title: ReasonPhrases.GetReasonPhrase(statusCode),
            extensions: errorCode is not null
                ? new Dictionary<string, object?> { ["errorCode"] = errorCode }
                : null);

        await problem.ExecuteAsync(httpContext);
        return true;
    }

    private void LogException(HttpContext httpContext, Exception exception, int statusCode, string? errorCode)
    {
        var requestPath = httpContext.Request.Path.Value;
        var requestMethod = httpContext.Request.Method;

        switch (statusCode)
        {
            case 503:
                // 外部サービス障害: サービス名、エンドポイント、詳細を記録
                var serviceName = exception switch
                {
                    ExternalServiceException e => e.ServiceName ?? "Unknown",
                    Stripe.StripeException => "Stripe",
                    _ => "External"
                };
                logger.LogError(exception,
                    "外部サービス障害: Service={ServiceName}, ErrorCode={ErrorCode}, " +
                    "RequestPath={RequestPath}, RequestMethod={RequestMethod}, Message={Message}",
                    serviceName, errorCode, requestPath, requestMethod, exception.Message);
                break;

            case >= 500:
                // 内部エラー: スタックトレースを含むフル情報
                logger.LogError(exception,
                    "Unhandled exception: ErrorCode={ErrorCode}, " +
                    "RequestPath={RequestPath}, RequestMethod={RequestMethod}, Message={Message}",
                    errorCode, requestPath, requestMethod, exception.Message);
                break;

            default:
                // ビジネス例外: Warning レベル
                logger.LogWarning(
                    "Handled exception: Type={ExceptionType}, ErrorCode={ErrorCode}, " +
                    "RequestPath={RequestPath}, RequestMethod={RequestMethod}, Message={Message}",
                    exception.GetType().Name, errorCode, requestPath, requestMethod, exception.Message);
                break;
        }
    }
}
