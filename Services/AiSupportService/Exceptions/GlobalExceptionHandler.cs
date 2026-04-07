using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace AiSupportService.Exceptions;

/// <summary>
/// RFC 9457 Problem Details 形式で統一的な例外レスポンスを生成するグローバル例外ハンドラー。
/// 例外の型に基づき適切な HTTP ステータスコードへマッピングし、
/// 500 系はエラーログ、それ以外は警告ログとして記録する。
/// </summary>
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken ct)
    {
        // 例外の型から HTTP ステータスコードとクライアント向けメッセージを決定
        var (statusCode, message) = exception switch
        {
            NotFoundException e => (404, e.Message),
            BusinessException e => (422, e.Message),
            UnauthorizedException => (401, "認証が必要です"),
            ForbiddenException => (403, "アクセスが拒否されました"),
            ConcurrencyException e => (409, e.Message),
            SecurityException e => (400, e.Message),
            AiServiceUnavailableException => (503, "AI サービスが一時的に利用できません"),
            _ => (500, "内部エラーが発生しました")
        };

        // 500 系はスタックトレース付きエラーログ、それ以外は警告ログ
        if (statusCode >= 500)
            logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        else
            logger.LogWarning("Handled exception: {ExceptionType} - {Message}",
                exception.GetType().Name, exception.Message);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Detail = message,
            Title = ReasonPhrases.GetReasonPhrase(statusCode)
        }, ct);
        return true;
    }
}
