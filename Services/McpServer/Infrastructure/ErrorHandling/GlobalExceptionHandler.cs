using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.ErrorHandling;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken ct)
    {
        var correlationId = httpContext.Response.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? httpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? httpContext.TraceIdentifier;

        var (statusCode, title, detail, logLevel) = exception switch
        {
            ValidationException validationException => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                validationException.Message,
                LogLevel.Warning),
            HttpRequestException => (
                StatusCodes.Status502BadGateway,
                "Bad Gateway",
                "Inventory catalog is temporarily unavailable.",
                LogLevel.Warning),
            TimeoutException => (
                StatusCodes.Status504GatewayTimeout,
                "Gateway Timeout",
                "Inventory catalog request timed out.",
                LogLevel.Warning),
            OperationCanceledException => (
                StatusCodes.Status504GatewayTimeout,
                "Gateway Timeout",
                "Inventory catalog request timed out.",
                LogLevel.Warning),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An internal server error occurred.",
                LogLevel.Error)
        };

        logger.Log(
            logLevel,
            exception,
            "Unhandled exception in MCP server. CorrelationId={CorrelationId}, TraceId={TraceId}, StatusCode={StatusCode}",
            correlationId,
            httpContext.TraceIdentifier,
            statusCode);

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, ct);

        return true;
    }
}
