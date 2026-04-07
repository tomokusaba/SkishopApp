// ─────────────────────────────────────────────────────────────
// RequiredHeaderMiddleware — 必須ヘッダー検証ミドルウェア
//
// 設計書 §11 エラーコード GW-4007 準拠。
// 指定パスプレフィックスに対して必須ヘッダーの存在を検証し、
// 欠落時は 400 + GW-4007 の RFC 9457 Problem Details を返す。
// ─────────────────────────────────────────────────────────────

using System.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;

namespace ApiGateway.Infrastructure.Middleware;

/// <summary>
/// 特定の API パスに対して必須ヘッダーの存在を検証するミドルウェア。
/// <para>
/// <c>Content-Type</c> ヘッダーが必須の POST/PUT/PATCH リクエストで、
/// ヘッダーが欠落している場合に GW-4007 エラーを返す。
/// </para>
/// </summary>
public sealed class RequiredHeaderMiddleware(
    RequestDelegate next,
    ILogger<RequiredHeaderMiddleware> logger)
{
    /// <summary>Content-Type ヘッダーが必須の HTTP メソッド。</summary>
    private static readonly HashSet<string> MethodsRequiringContentType =
        ["POST", "PUT", "PATCH"];

    /// <summary>
    /// API リクエストの必須ヘッダーを検証する。
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;

        // /api/ プレフィックスのリクエストのみ検証
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            && MethodsRequiringContentType.Contains(method)
            && !context.Request.HasJsonContentType()
            && context.Request.ContentLength > 0)
        {
            logger.LogWarning(
                "必須ヘッダー欠落: Content-Type, Method={Method}, Path={Path}",
                method, path);

            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                title = ReasonPhrases.GetReasonPhrase(400),
                status = 400,
                detail = "Content-Type ヘッダーが必要です（application/json）",
                instance = path,
                code = "GW-4007",
                traceId = Activity.Current?.Id ?? context.TraceIdentifier
            }, context.RequestAborted);
            return;
        }

        await next(context);
    }
}
