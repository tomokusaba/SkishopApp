using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using SalesManagementService.Infrastructure.Exceptions;

namespace SalesManagementService.Infrastructure.Security;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken ct)
    {
        var (statusCode, errorCode, message) = exception switch
        {
            NotFoundException e => (404, MapNotFoundErrorCode(e), e.Message),
            UnauthorizedException => (401, "ORD-4011", "認証が必要です"),
            ForbiddenException => (403, "ORD-4031", "アクセスが拒否されました"),
            InvalidOrderStateException e => (422, ErrorCodes.InvalidOrderStatusTransition, e.Message),
            InsufficientStockException e => (422, ErrorCodes.InsufficientStock, e.Message),
            PaymentProcessingException e => (422, ErrorCodes.PaymentFailed, e.Message),
            PaymentPendingException e => (202, ErrorCodes.PaymentPending, e.Message),
            ConcurrencyException e => (409, ErrorCodes.ConcurrencyConflict, e.Message),
            IdempotencyConflictException e => (409, ErrorCodes.IdempotencyConflict, e.Message),
            BusinessException e => (422, ErrorCodes.InvalidRequest, e.Message),
            ExternalServiceException e => (503, ErrorCodes.ExternalServiceUnavailable, e.Message),
            _ => (500, ErrorCodes.InternalServerError, "内部エラーが発生しました")
        };

        if (statusCode >= 500)
            logger.LogError(exception, "未処理の例外: ErrorCode={ErrorCode}, Message={Message}",
                errorCode, exception.Message);
        else
            logger.LogWarning("処理済み例外: ErrorCode={ErrorCode}, Type={ExceptionType}, Message={Message}",
                errorCode, exception.GetType().Name, exception.Message);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = message,
            Extensions = { ["errorCode"] = errorCode }
        }, ct);

        return true;
    }

    private static string MapNotFoundErrorCode(NotFoundException ex)
    {
        var msg = ex.Message.ToUpperInvariant();
        if (msg.Contains("ORDER ITEM") || msg.Contains("注文明細")) return ErrorCodes.OrderItemNotFound;
        if (msg.Contains("ORDER") || msg.Contains("注文")) return ErrorCodes.OrderNotFound;
        if (msg.Contains("SHIPMENT") || msg.Contains("出荷")) return ErrorCodes.ShipmentNotFound;
        if (msg.Contains("RETURN") || msg.Contains("返品")) return ErrorCodes.ReturnNotFound;
        if (msg.Contains("INVOICE") || msg.Contains("請求書")) return ErrorCodes.InvoiceNotFound;
        return ErrorCodes.OrderNotFound;
    }
}
