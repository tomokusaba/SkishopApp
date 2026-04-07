using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PointService.Exceptions;

namespace PointService.Infrastructure.Middleware;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (statusCode, detail, errorCode) = exception switch
        {
            NotFoundException => (404, "指定されたリソースが見つかりません", (string?)null),
            PointAccountNotFoundException e => (404, "ポイントアカウントが見つかりません", e.ErrorCode),
            InsufficientPointsException e => (422, "ポイント残高が不足しています", e.ErrorCode),
            PointExpiredException e => (422, "ポイントの有効期限が切れています", e.ErrorCode),
            DuplicateTransactionException e => (409, "重複するトランザクションが存在します", e.ErrorCode),
            ConcurrencyException => (409, "データが他のユーザーによって更新されました", (string?)null),
            BusinessException => (422, "ビジネスルール違反です", (string?)null),
            UnauthorizedException => (401, "認証が必要です", (string?)null),
            ForbiddenException => (403, "アクセスが拒否されました", (string?)null),
            _ => (500, "内部エラーが発生しました", (string?)null)
        };

        if (statusCode >= 500)
            logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        else
            logger.LogWarning("Handled exception: {ExceptionType} - {Message}",
                exception.GetType().Name, exception.Message);

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Detail = detail,
            Title = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(statusCode)
        };

        if (errorCode is not null)
            problemDetails.Extensions["errorCode"] = errorCode;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, ct);
        return true;
    }
}
