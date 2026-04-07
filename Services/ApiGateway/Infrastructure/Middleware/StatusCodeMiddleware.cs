// ─────────────────────────────────────────────────────────────
// StatusCodeMiddleware — 未処理 HTTP ステータスの RFC 9457 Problem Details 応答
//
// YARP 転送後の 404/405/415 等、ASP.NET Core パイプラインが生成する
// 未処理のステータスコードに対して、設計書 §11 のエラーコードを付与した
// RFC 9457 Problem Details 形式のレスポンスを返す。
// ─────────────────────────────────────────────────────────────

using System.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;

namespace ApiGateway.Infrastructure.Middleware;

/// <summary>
/// 未処理ステータスコード（404, 405, 415, 400）に対して
/// RFC 9457 Problem Details 形式のレスポンスを生成するミドルウェア。
/// <para>設計書 §11 のエラーコード GW-4004〜GW-4008 を付与する。</para>
/// </summary>
public sealed class StatusCodeMiddleware(
    RequestDelegate next,
    ILogger<StatusCodeMiddleware> logger)
{
    /// <summary>
    /// レスポンスのステータスコードを検査し、エラーコード付き Problem Details に変換する。
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        // 既にレスポンスボディが書き込まれている場合はスキップ
        if (context.Response.HasStarted) return;

        var statusCode = context.Response.StatusCode;
        var (gwCode, detail) = statusCode switch
        {
            404 => ("GW-4004", "指定されたルートが見つかりません"),
            405 => ("GW-4005", "指定された HTTP メソッドは許可されていません"),
            415 => ("GW-4006", "サポートされていないメディアタイプです"),
            400 when !context.Response.HasStarted => ("GW-4008", "リクエストの形式が不正です"),
            _ => (null, null)
        };

        if (gwCode is null) return;

        logger.LogWarning(
            "ステータスコード応答: Code={GwCode}, Status={StatusCode}, Path={Path}",
            gwCode, statusCode, context.Request.Path);

        // YARP がバックエンドの Content-Length をコピーしている場合、
        // Problem Details 書き込み前にクリアする（Content-Length 不整合防止）
        context.Response.Headers.Remove("Content-Length");
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = $"https://tools.ietf.org/html/rfc9110#section-15.5.{statusCode - 400 + 1}",
            title = ReasonPhrases.GetReasonPhrase(statusCode),
            status = statusCode,
            detail,
            instance = context.Request.Path.Value,
            code = gwCode,
            traceId = Activity.Current?.Id ?? context.TraceIdentifier
        }, context.RequestAborted);
    }
}
