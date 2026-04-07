using InventoryManagementService.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementService.Infrastructure.Middleware;

/// <summary>
/// グローバル例外ハンドラー。IExceptionHandler を実装し、
/// ドメイン例外を RFC 9457 ProblemDetails 形式のレスポンスにマッピングする。
/// </summary>
/// <param name="logger">ロガー</param>
/// <remarks>
/// 例外マッピング:
/// - ResourceNotFoundException → 404 Not Found
/// - InsufficientStockException → 422 Unprocessable Entity
/// - DuplicateResourceException → 409 Conflict
/// - ConcurrencyException → 409 Conflict
/// - InventoryException → 422 Unprocessable Entity
/// - UnauthorizedAccessException → 401 Unauthorized
/// - DbUpdateConcurrencyException → 409 Conflict
/// - その他 → 500 Internal Server Error
///
/// HttpContext.Items["CorrelationId"] から相関IDを取得し、レスポンスヘッダーと ProblemDetails に含める。
/// Response.Clear() 後も HttpContext.Items は生存するため、CorrelationId の伝搬が保証される。
/// </remarks>
public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>
    /// 例外を処理し、ProblemDetails 形式のレスポンスを生成する。
    /// </summary>
    /// <param name="httpContext">HTTPコンテキスト</param>
    /// <param name="exception">発生した例外</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>例外が処理された場合は true</returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, message) = exception switch
        {
            ResourceNotFoundException e => (404, e.Message),
            InsufficientStockException e => (422, e.Message),
            DuplicateResourceException e => (409, e.Message),
            ConcurrencyException e => (409, e.Message),
            InventoryException e => (422, e.Message),
            UnauthorizedAccessException => (401, "認証が必要です"),
            DbUpdateConcurrencyException => (409,
                "データが他のユーザーによって更新されました。再度お試しください。"),
            _ => (500, "内部エラーが発生しました")
        };

        if (statusCode >= 500)
            logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        else
            logger.LogWarning("Handled exception: {ExceptionType} - {Message}",
                exception.GetType().Name, exception.Message);

        httpContext.Response.StatusCode = statusCode;

        var correlationId = httpContext.Items["CorrelationId"]?.ToString() ?? "";
        if (!httpContext.Response.Headers.ContainsKey("X-Correlation-Id"))
            httpContext.Response.Headers.Append("X-Correlation-Id", correlationId);

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Detail = message,
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Type = $"https://httpstatuses.io/{statusCode}",
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["traceId"] = httpContext.TraceIdentifier,
                ["correlationId"] = correlationId
            }
        }, cancellationToken);

        return true;
    }
}
